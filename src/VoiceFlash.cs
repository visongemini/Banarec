using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

sealed class FlashAsrException : InvalidOperationException {
 public string StatusCode {get;private set;}
 public FlashAsrException(string code,string message):base(message){StatusCode=code??"";}
}

static class Pcm16Wave {
 public static byte[] Wrap16KhzMono(byte[] pcm) {
  if(pcm==null||pcm.Length==0)throw new ArgumentException("没有可识别的音频。","pcm");
  if((pcm.Length&1)!=0)throw new ArgumentException("PCM16 音频字节数必须为偶数。","pcm");
  if(pcm.Length>int.MaxValue-44)throw new ArgumentException("音频过大。","pcm");
  using(var stream=new MemoryStream(44+pcm.Length))using(var writer=new BinaryWriter(stream,Encoding.ASCII,true)) {
   writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+pcm.Length);
   writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);
   writer.Write((short)1);writer.Write(16000);writer.Write(32000);writer.Write((short)2);writer.Write((short)16);
   writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(pcm.Length);writer.Write(pcm);
   writer.Flush();return stream.ToArray();
  }
 }
}

static class VoiceFlashCredentials {
 public const string Backend="flash";
 public const string DefaultEndpoint="https://openspeech.bytedance.com/api/v3/auc/bigmodel/recognize/flash";
 public const string DefaultResourceId="volc.bigasr.auc_turbo";
 static string DefaultFile {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".config","cowork-llm","keys.env");}}

 public static VoiceOptions ImportAndSaveDefault() {
  var options=VoiceData.LoadOptions();
  ApplyFromFile(options,DefaultFile);
  VoiceData.SaveOptions(options);
  return options;
 }

 public static void ApplyFromFile(VoiceOptions options,string path) {
  if(options==null)throw new ArgumentNullException("options");
  string appId=null,accessToken=null;
  try {
   var file=new FileInfo(path);
   if(!file.Exists)throw new FileNotFoundException();
   if(file.Length>64*1024)throw new InvalidDataException();
   foreach(string raw in File.ReadAllLines(path,new UTF8Encoding(false,true))) {
    string line=(raw??"").Trim();if(line.Length==0||line.StartsWith("#",StringComparison.Ordinal))continue;
    int equals=line.IndexOf('=');if(equals<1)continue;
    string key=line.Substring(0,equals).Trim();
    if(key!="VOLC_ASR_APP_ID"&&key!="VOLC_ASR_ACCESS_TOKEN")continue;
    string value=Unquote(line.Substring(equals+1).Trim());
    if(key=="VOLC_ASR_APP_ID")appId=value;else accessToken=value;
   }
  } catch(InvalidOperationException){throw;} catch {throw new InvalidOperationException("无法读取豆包语音凭据文件。");}
  if(string.IsNullOrWhiteSpace(appId)||string.IsNullOrWhiteSpace(accessToken))throw new InvalidOperationException("豆包语音凭据文件缺少必需配置。");
  options.ApiKey="";
  options.AppKey=appId;
  options.AccessKey=accessToken;
  options.Endpoint=DefaultEndpoint;
  options.ResourceId=DefaultResourceId;
 }

 static string Unquote(string value) {
  if(value.Length>=2&&((value[0]=='"'&&value[value.Length-1]=='"')||(value[0]=='\''&&value[value.Length-1]=='\'')))return value.Substring(1,value.Length-2);
  return value;
 }
}

sealed class FlashAsrClient {
 public const string Backend=VoiceFlashCredentials.Backend;
 public const string Endpoint=VoiceFlashCredentials.DefaultEndpoint;
 public const string ResourceId=VoiceFlashCredentials.DefaultResourceId;
 const string Success="20000000";
 const int TimeoutMilliseconds=60000;
 readonly VoiceOptions options;
 readonly List<string> hotwords;
 readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=16*1024*1024};

 public FlashAsrClient(VoiceOptions options,List<string> hotwords) {
  if(options==null)throw new ArgumentNullException("options");
  if(string.IsNullOrWhiteSpace(options.AppKey)||string.IsNullOrWhiteSpace(options.AccessKey))throw new InvalidOperationException("请先配置豆包 Flash 语音凭据。");
  this.options=options;
  this.hotwords=(hotwords??new List<string>()).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim()).Distinct(StringComparer.Ordinal).Take(1000).ToList();
 }

 public async Task<string> Recognize(byte[] pcm,CancellationToken cancellationToken) {
  cancellationToken.ThrowIfCancellationRequested();
  byte[] wav=Pcm16Wave.Wrap16KhzMono(pcm);
  string context=json.Serialize(new Dictionary<string,object>{{"hotwords",hotwords.Select(x=>(object)new Dictionary<string,object>{{"word",x}}).ToArray()}});
  var requestBody=new Dictionary<string,object>{
   {"user",new Dictionary<string,object>{{"uid","BanaStudio"}}},
   {"audio",new Dictionary<string,object>{{"format","wav"},{"data",Convert.ToBase64String(wav)}}},
   {"request",new Dictionary<string,object>{{"model_name","bigmodel"},{"corpus",new Dictionary<string,object>{{"context",context}}}}}
  };
  byte[] body=Encoding.UTF8.GetBytes(json.Serialize(requestBody));
  var request=(HttpWebRequest)WebRequest.Create(Endpoint);
  request.Method="POST";request.ContentType="application/json";request.ContentLength=body.Length;
  request.Timeout=TimeoutMilliseconds;request.ReadWriteTimeout=TimeoutMilliseconds;
  request.Headers["X-Api-App-Key"]=options.AppKey;
  request.Headers["X-Api-Access-Key"]=options.AccessKey;
  request.Headers["X-Api-Resource-Id"]=ResourceId;
  request.Headers["X-Api-Request-Id"]=Guid.NewGuid().ToString();
  request.Headers["X-Api-Sequence"]="-1";
  try {
   using(cancellationToken.Register(request.Abort)) {
    using(var stream=await request.GetRequestStreamAsync())await stream.WriteAsync(body,0,body.Length,cancellationToken);
    using(var response=(HttpWebResponse)await request.GetResponseAsync())return ReadResponse(response);
   }
  } catch(WebException error) {
   if(cancellationToken.IsCancellationRequested)throw new OperationCanceledException(cancellationToken);
   using(var response=error.Response as HttpWebResponse) {
    string code=response==null?"":response.Headers["X-Api-Status-Code"];
    throw new FlashAsrException(code,DescribeFailure(code,response==null?null:(int?)response.StatusCode));
   }
  }
 }

 string ReadResponse(HttpWebResponse response) {
  string code=response.Headers["X-Api-Status-Code"]??"";
  if(code!=Success)throw new FlashAsrException(code,DescribeFailure(code,(int)response.StatusCode));
  string payload;
  using(var stream=response.GetResponseStream())using(var reader=new StreamReader(stream,Encoding.UTF8,true,4096,false))payload=reader.ReadToEnd();
  try {
   var root=json.DeserializeObject(payload) as Dictionary<string,object>;
   object resultObject;if(root==null||!root.TryGetValue("result",out resultObject))return "";
   var result=resultObject as Dictionary<string,object>;if(result==null)return "";
   object textObject;if(result.TryGetValue("text",out textObject)){string text=Convert.ToString(textObject).Trim();if(text.Length>0)return text;}
   object utterancesObject;if(!result.TryGetValue("utterances",out utterancesObject))return "";
   var utterances=utterancesObject as object[];if(utterances==null)return "";
   return string.Concat(utterances.Select(x=>{var item=x as Dictionary<string,object>;object value;return item!=null&&item.TryGetValue("text",out value)?Convert.ToString(value):"";})).Trim();
  } catch {throw new FlashAsrException("invalid-response","豆包语音服务返回了无法解析的结果。");}
 }

 static string DescribeFailure(string code,int? httpStatus) {
  switch(code) {
   case "20000001":return "豆包语音任务仍在处理中。";
   case "20000002":return "豆包语音任务不存在。";
   case "20000003":return "没有识别到语音。";
   case "45000001":return "豆包语音请求参数无效。";
   case "45000002":return "音频为空或格式不正确。";
   case "45000151":return "音频超过豆包语音服务允许的时长。";
   case "55000000":return "豆包语音服务暂时不可用。";
  }
  return httpStatus.HasValue?"豆包语音服务请求失败（HTTP "+httpStatus.Value+"）。":"无法连接豆包语音服务。";
 }
}
