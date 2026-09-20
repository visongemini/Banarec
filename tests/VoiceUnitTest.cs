using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

class VoiceUnitTest {
 static int passed,failed;
 static string root;

 static void Main() {
  root=Path.Combine(Path.GetTempPath(),"BanaStudio-voice-tests-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(root);
  try {
   Run("options defaults",OptionsDefaults);
   Run("flash PCM WAV wrapping",FlashWaveWrapping);
   Run("flash credential file boundaries",FlashCredentialBoundaries);
   Run("session safe idle paths",SessionSafeIdlePaths);
   Run("session generation rejects late failure",SessionRejectsLateFailure);
   Run("session current failure returns idle",SessionFailureReturnsIdle);
   Run("session stop is single and reusable",SessionStopSingleGate);
   Run("session remembers stop during starting",SessionStartingStopRemembered);
   Run("session background failure returns to captured context",SessionCapturedContext);
   Run("session queue and flash deadlines are bounded",SessionBounds);
   Run("session cancel and stale generation block delivery",SessionCancelBlocksDelivery);
   Run("session old generation cannot change new counters",SessionGenerationCounters);
   Run("final result gate rejects timeout, cancel and fault",FinalResultGate);
   Run("corrections are single pass and longest first",CorrectionsSinglePass);
   Run("corrections detect ASCII ambiguity and boundaries",CorrectionsAsciiAmbiguity);
   Run("corrections ignore disabled terms",CorrectionsDisabled);
   Run("protocol final flags and definite",ProtocolFinalSignals);
   Run("protocol cumulative text replaces preview",ProtocolCumulativeText);
   Run("protocol result list and unknown result",ProtocolResultShapes);
   Run("protocol error, truncated, empty final",ProtocolBoundaries);
   Run("protocol malformed gzip reports failure",ProtocolMalformedGzip);
   Run("JSON round trip",JsonRoundTrip);
   Run("JSON rejects malformed and unknown schema",JsonRejectsInvalid);
   Run("CSV quoted fields and multiline",CsvQuotedFields);
   Run("CSV UTF-8 BOM and single column",CsvBomAndSingleColumn);
   Run("CSV formula safety preserves word",CsvFormulaRoundTrip);
   Run("TXT trim, blanks, duplicate and case",TxtBoundaries);
   Run("term and collection limits",Limits);
   Run("import size and cross-batch merge limits",ImportAndMergeLimits);
   Run("merge aliases and enabled state",Merge);
   Run("unsupported extension",UnsupportedExtension);
  } finally {
   try {Directory.Delete(root,true);} catch {}
  }
  Console.WriteLine("Voice unit tests: "+passed+" passed, "+failed+" failed.");
  if(failed!=0)Environment.ExitCode=1;
 }

 static void Run(string name,Action test) {
  try {test();passed++;Console.WriteLine("PASS "+name);}
  catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}
 }
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Equal<T>(T expected,T actual,string message){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception(message+" (expected "+expected+", got "+actual+")");}
 static void Throws<T>(Action action,string message) where T:Exception {try{action();}catch(T){return;}catch(Exception e){throw new Exception(message+" (got "+e.GetType().Name+")");}throw new Exception(message+" (no exception)");}
 static string FilePath(string name){return Path.Combine(root,name);}

 static VoiceTerm Term(string word,IEnumerable<string> aliases=null,bool enabled=true,string source="manual") {
  var value=VoiceData.NewTerm(word,aliases==null?new List<string>():aliases.ToList(),source);
  value.enabled=enabled;
  return value;
 }

 static void OptionsDefaults() {
  var value=new VoiceOptions();
  Check(!value.Ready,"blank credentials must not be ready");
  Check(value.SaveHistory,"history default");
  Equal(30,value.RetentionDays,"retention default");
  Equal(300,value.MaxSeconds,"duration default");
  Equal(0x20,value.VirtualKey,"voice hotkey default key");
  value.AppKey="app";value.AccessKey="token";
  Check(value.Ready,"complete credentials should be ready");
 }

 static int Int32(byte[] value,int offset){return BitConverter.ToInt32(value,offset);}
 static short Int16(byte[] value,int offset){return BitConverter.ToInt16(value,offset);}
 static void FlashWaveWrapping() {
  byte[] pcm={1,2,3,4};byte[] wav=Pcm16Wave.Wrap16KhzMono(pcm);
  Equal("RIFF",Encoding.ASCII.GetString(wav,0,4),"RIFF header");
  Equal(40,Int32(wav,4),"RIFF size");
  Equal("WAVEfmt ",Encoding.ASCII.GetString(wav,8,8),"WAVE format marker");
  Equal((short)1,Int16(wav,20),"PCM encoding");Equal((short)1,Int16(wav,22),"mono");Equal(16000,Int32(wav,24),"sample rate");Equal((short)16,Int16(wav,34),"sample bits");
  Equal("data",Encoding.ASCII.GetString(wav,36,4),"data marker");Equal(pcm.Length,Int32(wav,40),"data size");Check(wav.Skip(44).SequenceEqual(pcm),"PCM payload");
  Throws<ArgumentException>(()=>Pcm16Wave.Wrap16KhzMono(new byte[0]),"empty PCM");
  Throws<ArgumentException>(()=>Pcm16Wave.Wrap16KhzMono(new byte[1]),"odd PCM16 byte count");
  Throws<InvalidOperationException>(()=>new FlashAsrClient(new VoiceOptions(),new List<string>()),"blank flash credentials");
 }
 static void FlashCredentialBoundaries() {
  string path=FilePath("keys.env");
  File.WriteAllText(path,"# ignored\nVOLC_ASR_APP_ID=\" app-id \"\nUNKNOWN=nope\nVOLC_ASR_ACCESS_TOKEN='token-value'\n",new UTF8Encoding(false));
  var options=new VoiceOptions{ApiKey="must-clear",Endpoint="old",ResourceId="old"};
  VoiceFlashCredentials.ApplyFromFile(options,path);
  Equal(" app-id ",options.AppKey,"quoted app id content");Equal("token-value",options.AccessKey,"single quoted token");Equal("",options.ApiKey,"new-console key cleared");
  Equal(VoiceFlashCredentials.DefaultEndpoint,options.Endpoint,"flash endpoint");Equal(VoiceFlashCredentials.DefaultResourceId,options.ResourceId,"flash resource");
  string missing=FilePath("missing.env");File.WriteAllText(missing,"VOLC_ASR_APP_ID=only",new UTF8Encoding(false));
  Throws<InvalidOperationException>(()=>VoiceFlashCredentials.ApplyFromFile(new VoiceOptions(),missing),"missing access token");
  Throws<InvalidOperationException>(()=>VoiceFlashCredentials.ApplyFromFile(new VoiceOptions(),FilePath("absent.env")),"missing credential file");
  string huge=FilePath("huge.env");File.WriteAllText(huge,new string('x',65*1024),new UTF8Encoding(false));
  Throws<InvalidOperationException>(()=>VoiceFlashCredentials.ApplyFromFile(new VoiceOptions(),huge),"oversized credential file");
 }

 static byte[] Gzip(byte[] value) {
  using(var output=new MemoryStream()){
   using(var gzip=new GZipStream(output,CompressionMode.Compress,true))gzip.Write(value,0,value.Length);
   return output.ToArray();
  }
 }
 static void Write32(byte[] value,int offset,int number){value[offset]=(byte)(number>>24);value[offset+1]=(byte)(number>>16);value[offset+2]=(byte)(number>>8);value[offset+3]=(byte)number;}
 static byte[] ServerFrame(int flags,string json,bool compressed=true,int sequence=1) {
  var payload=Encoding.UTF8.GetBytes(json);
  if(compressed)payload=Gzip(payload);
  int sequenceSize=(flags&1)!=0?4:0;
  var frame=new byte[4+sequenceSize+4+payload.Length];
  frame[0]=0x11;frame[1]=(byte)(0x90|flags);frame[2]=(byte)(0x10|(compressed?1:0));
  int offset=4;if(sequenceSize!=0){Write32(frame,offset,sequence);offset+=4;}Write32(frame,offset,payload.Length);Buffer.BlockCopy(payload,0,frame,offset+4,payload.Length);
  return frame;
 }
 static byte[] ErrorFrame(int code,string json) {
  var payload=Encoding.UTF8.GetBytes(json);var frame=new byte[12+payload.Length];frame[0]=0x11;frame[1]=0xF0;frame[2]=0x10;Write32(frame,4,code);Write32(frame,8,payload.Length);Buffer.BlockCopy(payload,0,frame,12,payload.Length);return frame;
 }
 static DoubaoAsrClient Client(Action<string,bool> text,Action<string> failed) {
  var client=new DoubaoAsrClient(new VoiceOptions(),new string[0]);client.Text+=text;client.Failed+=failed;return client;
 }
 static void Parse(DoubaoAsrClient client,byte[] frame) {
  typeof(DoubaoAsrClient).GetMethod("Parse",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(client,new object[]{frame});
 }

 static void ProtocolFinalSignals() {
  var values=new List<Tuple<string,bool>>();var errors=new List<string>();using(var client=Client((t,f)=>values.Add(Tuple.Create(t,f)),errors.Add)){
   Parse(client,ServerFrame(1,"{\"result\":{\"text\":\"预览\",\"definite\":false}}"));
   Parse(client,ServerFrame(1,"{\"result\":{\"text\":\"分句已定\",\"definite\":true}}"));
   Parse(client,ServerFrame(3,"{\"result\":{\"text\":\"整段最终\",\"definite\":false}}",true,-3));
  }
  Equal(3,values.Count,"three text callbacks");
  Check(!values[0].Item2,"ordinary response is preview");
  Check(!values[1].Item2,"definite closes one utterance but is not the terminal stream frame");
  Check(values[2].Item2,"terminal frame flag is final");
  Equal(0,errors.Count,"no parser errors");
 }

 static void ProtocolCumulativeText() {
  var values=new List<string>();using(var client=Client((t,f)=>values.Add(t),e=>{})){
   Parse(client,ServerFrame(1,"{\"result\":{\"text\":\"你好\"}}"));
   Parse(client,ServerFrame(1,"{\"result\":{\"text\":\"你好世界\"}}"));
  }
  Check(values.SequenceEqual(new[]{"你好","你好世界"}),"full cumulative results must replace preview instead of being appended");
 }

 static void ProtocolResultShapes() {
  var values=new List<string>();var errors=new List<string>();using(var client=Client((t,f)=>values.Add(t),errors.Add)){
   Parse(client,ServerFrame(1,"{\"result\":[{\"text\":\"甲\"},{\"text\":\"乙\"}]}"));
   Parse(client,ServerFrame(1,"{\"result\":[1,2]}"));
  }
  Check(values.SequenceEqual(new[]{"甲乙"}),"official wrapper list must produce text without object type names");
  Equal(1,errors.Count,"unknown result objects must fail explicitly");
  Check(!values.Any(x=>x.Contains("System.Object")),"runtime type names are never transcripts");
 }

 static void ProtocolBoundaries() {
  var values=new List<Tuple<string,bool>>();var errors=new List<string>();using(var client=Client((t,f)=>values.Add(Tuple.Create(t,f)),errors.Add)){
   Parse(client,new byte[]{0x11,0x91,0x11});
   Parse(client,ServerFrame(3,"{\"result\":{\"text\":\"\"}}",true,-1));
   Parse(client,ErrorFrame(45000001,"{\"message\":\"bad request\"}"));
  }
  Equal(1,values.Count,"empty terminal frame must still signal completion");
  Equal("",values[0].Item1,"empty terminal text");
  Check(values[0].Item2,"empty terminal frame final flag");
  Equal(2,errors.Count,"truncated frame and service error each produce one failure callback");
  Equal(1,errors.Count(x=>x.Contains("45000001")&&x.Contains("bad request")),"service error is reported exactly once");
  Equal(1,errors.Count(x=>x.Contains("不完整")||x.Contains("无效")),"truncated frame is rejected exactly once");
 }

 static void ProtocolMalformedGzip() {
  var errors=new List<string>();using(var client=Client((t,f)=>{},errors.Add)){
   var frame=new byte[]{0x11,0x91,0x11,0,0,0,0,1,0,0,0,3,1,2,3};
   Parse(client,frame);
  }
  Equal(1,errors.Count,"malformed compressed payload should report failure without escaping parser callback");
 }

 static void SessionSafeIdlePaths() {
  var session=new VoiceSession(new VoiceOptions(),new List<VoiceTerm>());
  Equal(VoiceState.Idle,session.State,"new session state");
  session.Stop().GetAwaiter().GetResult();
  Equal(VoiceState.Idle,session.State,"stopping idle is a no-op");
  Throws<InvalidOperationException>(()=>session.Start(false).GetAwaiter().GetResult(),"missing credentials must fail before network or microphone access");
  Equal(VoiceState.Idle,session.State,"failed precondition must remain idle");
  session.Cancel();session.Cancel();session.Dispose();
  Equal(VoiceState.Idle,session.State,"cancel and dispose are idempotent while idle");
 }

 static void SessionRejectsLateFailure() {
  var session=new VoiceSession(new VoiceOptions(),new List<VoiceTerm>());
  var type=typeof(VoiceSession);
  type.GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,7);
  type.GetMethod("Change",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{VoiceState.Listening,"current",0d});
  type.GetMethod("Fail",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{6,"late"});
  Equal(VoiceState.Listening,session.State,"late callback must not change current state");
  Equal("current",session.Preview,"late callback must not replace current preview");
  session.Cancel();
  Equal(VoiceState.Idle,session.State,"cancel returns active state to idle");
  type.GetMethod("Fail",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{7,"late after cancel"});
  Equal(VoiceState.Idle,session.State,"callback from cancelled generation remains ignored");
  Equal("",session.Preview,"callback from cancelled generation does not restore draft");
 }

 static void SessionFailureReturnsIdle() {
  var session=new VoiceSession(new VoiceOptions{SaveHistory=false},new List<VoiceTerm>());var type=typeof(VoiceSession);var states=new List<VoiceState>();session.Changed+=(s,t,l)=>states.Add(s);
  type.GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,3);
  type.GetMethod("Change",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{VoiceState.Listening,"draft",0d});
  type.GetMethod("Fail",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{3,"network failed"});
  Equal(VoiceState.Idle,session.State,"current failure must unlock the session");Equal("network failed",session.Preview,"error text remains visible");Check(states.Count>=3&&states[states.Count-2]==VoiceState.Error&&states[states.Count-1]==VoiceState.Idle,"failure must notify Error and then Idle so controllers unlock");
  Throws<InvalidOperationException>(()=>session.Start(false).GetAwaiter().GetResult(),"next begin reaches credential validation instead of being ignored");
  Equal(VoiceState.Idle,session.State,"failed retry remains reusable");
 }

 static void SessionStopSingleGate() {
  var session=new VoiceSession(new VoiceOptions{SaveHistory=false},new List<VoiceTerm>());var type=typeof(VoiceSession);int finalizing=0;
  session.Changed+=(s,t,l)=>{if(s==VoiceState.Finalizing)finalizing++;};type.GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,9);type.GetMethod("Change",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{VoiceState.Listening,"",0d});
  session.Stop().GetAwaiter().GetResult();session.Stop().GetAwaiter().GetResult();
  Equal(1,finalizing,"concurrent/repeated stop enters finalizing once");Equal(VoiceState.Idle,session.State,"empty capture returns idle");
  Throws<InvalidOperationException>(()=>session.Start(false).GetAwaiter().GetResult(),"a new begin is evaluated after completed stop");Equal(VoiceState.Idle,session.State,"retry remains idle after credential validation");
 }
 static void SessionStartingStopRemembered() {
  var session=new VoiceSession(new VoiceOptions{SaveHistory=false},new List<VoiceTerm>());var type=typeof(VoiceSession);type.GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,2);type.GetMethod("Change",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{VoiceState.Starting,"",0d});
  session.Stop().GetAwaiter().GetResult();session.Stop().GetAwaiter().GetResult();
  Equal(1,(int)type.GetField("stopPending",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session),"starting stop is remembered");Equal(1,(int)type.GetField("stopping",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session),"starting stop uses one gate");session.Cancel();Equal(VoiceState.Idle,session.State,"cancelled starting session returns idle");
 }
 sealed class PumpContext:System.Threading.SynchronizationContext {
  readonly Queue<System.Threading.SendOrPostCallback> callbacks=new Queue<System.Threading.SendOrPostCallback>();readonly Queue<object> values=new Queue<object>();
  public override void Post(System.Threading.SendOrPostCallback callback,object state){lock(callbacks){callbacks.Enqueue(callback);values.Enqueue(state);}}
  public void Drain(){while(true){System.Threading.SendOrPostCallback callback;object value;lock(callbacks){if(callbacks.Count==0)return;callback=callbacks.Dequeue();value=values.Dequeue();}callback(value);}}
 }
 static void SessionCapturedContext() {
  var previous=System.Threading.SynchronizationContext.Current;var pump=new PumpContext();System.Threading.SynchronizationContext.SetSynchronizationContext(pump);
  try{var session=new VoiceSession(new VoiceOptions{SaveHistory=false},new List<VoiceTerm>());var type=typeof(VoiceSession);type.GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,4);type.GetMethod("Change",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{VoiceState.Listening,"draft",0d});System.Threading.Tasks.Task.Run(()=>type.GetMethod("Fail",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{4,"background failure"})).Wait();Equal(VoiceState.Listening,session.State,"background callback does not mutate UI state before dispatch");pump.Drain();Equal(VoiceState.Idle,session.State,"dispatched failure unlocks session");Equal("background failure",session.Preview,"dispatched error text");}
  finally{System.Threading.SynchronizationContext.SetSynchronizationContext(previous);}
 }
 static void SessionBounds(){Check(VoiceSession.MaxQueuedAudio>0&&VoiceSession.MaxQueuedAudio<=64,"audio send queue has a small hard bound");Equal(60000,VoiceSession.FlashTimeoutMilliseconds,"flash request has a 60 second deadline");}
 static void SessionCancelBlocksDelivery() {
  var session=new VoiceSession(new VoiceOptions{SaveHistory=false},new List<VoiceTerm>());var type=typeof(VoiceSession);var deliver=type.GetMethod("Deliver",BindingFlags.Instance|BindingFlags.NonPublic);type.GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,11);
  using(var cancelled=new System.Threading.CancellationTokenSource()){cancelled.Cancel();Throws<OperationCanceledException>(()=>((System.Threading.Tasks.Task)deliver.Invoke(session,new object[]{11,null,"must not reach clipboard",cancelled.Token})).GetAwaiter().GetResult(),"cancelled delivery is rejected before clipboard or paste");}
  using(var active=new System.Threading.CancellationTokenSource()){Throws<OperationCanceledException>(()=>((System.Threading.Tasks.Task)deliver.Invoke(session,new object[]{10,null,"late generation",active.Token})).GetAwaiter().GetResult(),"stale generation is rejected before clipboard or paste");}
  Equal(VoiceState.Idle,session.State,"delivery gate does not alter session state");
 }
 static void SessionGenerationCounters() {
  var session=new VoiceSession(new VoiceOptions{SaveHistory=false},new List<VoiceTerm>());var type=typeof(VoiceSession);type.GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,21);type.GetField("queuedAudioGeneration",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,21);type.GetField("queuedAudio",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,5);var release=type.GetMethod("ReleaseQueuedAudio",BindingFlags.Instance|BindingFlags.NonPublic);release.Invoke(session,new object[]{20});Equal(5,(int)type.GetField("queuedAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session),"old send completion must not decrement the current queue");release.Invoke(session,new object[]{21});Equal(4,(int)type.GetField("queuedAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session),"current send completion releases one queue slot");
  type.GetField("acceptingAudio",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,1);type.GetField("acceptingAudioGeneration",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,21);var stop=type.GetMethod("StopAcceptingAudio",BindingFlags.Instance|BindingFlags.NonPublic);stop.Invoke(session,new object[]{20});Equal(1,(int)type.GetField("acceptingAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session),"old stop finally must not disable current capture");stop.Invoke(session,new object[]{21});Equal(0,(int)type.GetField("acceptingAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session),"current stop disables its own capture");
 }

 static void FinalResultGate() {
  VoiceSession.RequireFinal(System.Threading.Tasks.Task.FromResult(0),50,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
  var cancelled=new System.Threading.Tasks.TaskCompletionSource<bool>();cancelled.SetCanceled();
  Throws<OperationCanceledException>(()=>VoiceSession.RequireFinal(cancelled.Task,50,System.Threading.CancellationToken.None).GetAwaiter().GetResult(),"cancelled final must abort delivery");
  var faulted=new System.Threading.Tasks.TaskCompletionSource<bool>();faulted.SetException(new InvalidOperationException("broken final"));
  Throws<InvalidOperationException>(()=>VoiceSession.RequireFinal(faulted.Task,50,System.Threading.CancellationToken.None).GetAwaiter().GetResult(),"faulted final must abort delivery");
  var pending=new System.Threading.Tasks.TaskCompletionSource<bool>();
  Throws<TimeoutException>(()=>VoiceSession.RequireFinal(pending.Task,1,System.Threading.CancellationToken.None).GetAwaiter().GetResult(),"missing final must time out before delivery");
 }

 static void CorrectionsSinglePass() {
  var terms=new List<VoiceTerm>{Term("B",new[]{"A"}),Term("C",new[]{"B"}),Term("琵琶雅礼",new[]{"琵琶牙里"}),Term("短词",new[]{"琵琶"})};
  Equal("B 琵琶雅礼",VoiceCorrections.Apply("A 琵琶牙里",terms),"aliases are matched once against original text, with longest alias first");
 }
 static void CorrectionsAsciiAmbiguity() {
  var terms=new List<VoiceTerm>{Term("First",new[]{"Foo"}),Term("Second",new[]{"foo"})};
  Equal("Foo foo food",VoiceCorrections.Apply("Foo foo food",terms),"case-insensitive ASCII aliases with different targets are ambiguous and must remain unchanged");
  Equal("a First b food",VoiceCorrections.Apply("a Foo b food",new[]{Term("First",new[]{"foo"})}),"ASCII aliases use word boundaries and ignore case");
 }
 static void CorrectionsDisabled() {
  Equal("旧词",VoiceCorrections.Apply("旧词",new[]{Term("新词",new[]{"旧词"},false)}),"disabled correction is ignored");
 }

 static void JsonRoundTrip() {
  string path=FilePath("roundtrip.json");
  var source=new[]{Term("琵琶雅礼",new[]{"琵琶牙里","琵琶雅丽"},false,"import"),Term("BanaStudio",new[]{"Banarec"})};
  VoiceData.ExportJson(path,source);
  byte[] bytes=File.ReadAllBytes(path);
  Check(bytes.Length>3&&!(bytes[0]==0xEF&&bytes[1]==0xBB&&bytes[2]==0xBF),"JSON export should be UTF-8 without BOM");
  var actual=VoiceData.Import(path);
  Equal(2,actual.Count,"term count");
  Equal("琵琶雅礼",actual[0].word,"Chinese word");
  Equal(false,actual[0].enabled,"enabled state");
  Equal("import",actual[0].source,"source");
  Check(actual[0].aliases.SequenceEqual(source[0].aliases),"aliases");
 }

 static void JsonRejectsInvalid() {
  string malformed=FilePath("malformed.json"),schema=FilePath("schema.json"),missing=FilePath("missing.json");
  File.WriteAllText(malformed,"{not json",new UTF8Encoding(false));
  File.WriteAllText(schema,"{\"schemaVersion\":2,\"terms\":[]}",new UTF8Encoding(false));
  File.WriteAllText(missing,"{\"schemaVersion\":1}",new UTF8Encoding(false));
  Throws<Exception>(()=>VoiceData.Import(malformed),"malformed JSON");
  Throws<InvalidDataException>(()=>VoiceData.Import(schema),"unknown schema");
  Throws<InvalidDataException>(()=>VoiceData.Import(missing),"missing terms");
 }

 static void CsvQuotedFields() {
  string path=FilePath("quoted.csv");
  File.WriteAllText(path,"word,aliases,enabled\r\n\"品牌,甲\",\"别名\"\"一|跨\r\n行\",false\r\n",new UTF8Encoding(false));
  var actual=VoiceData.Import(path);
  Equal(1,actual.Count,"quoted row count");
  Equal("品牌,甲",actual[0].word,"quoted comma");
  Equal(false,actual[0].enabled,"enabled parse");
  Check(actual[0].aliases.SequenceEqual(new[]{"别名\"一","跨\r\n行"}),"escaped quote and embedded newline");
  string broken=FilePath("broken.csv");
  File.WriteAllText(broken,"word\n\"unclosed",new UTF8Encoding(false));
  Throws<InvalidDataException>(()=>VoiceData.Import(broken),"unclosed CSV quote");
 }

 static void CsvBomAndSingleColumn() {
  string bom=FilePath("bom.csv");
  File.WriteAllText(bom,"word,aliases,enabled\r\n睡蕉,,true\r\n",new UTF8Encoding(true));
  Equal("睡蕉",VoiceData.Import(bom).Single().word,"BOM header");
  string one=FilePath("one.csv");
  File.WriteAllText(one,"Banarec\r\nBanaStudio\r\n",new UTF8Encoding(false));
  Check(VoiceData.Import(one).Select(x=>x.word).SequenceEqual(new[]{"Banarec","BanaStudio"}),"headerless one-column CSV");
 }

 static void CsvFormulaRoundTrip() {
  string path=FilePath("formula.csv");
  var words=new[]{"=1+1","+SUM(A1:A2)","-2+3","@cmd"};
  VoiceData.ExportCsv(path,words.Select(x=>Term(x)));
  string exported=File.ReadAllText(path,Encoding.UTF8);
  Check(words.All(x=>!exported.Contains("\""+x+"\"")),"formula-like cells must be neutralized");
  Check(VoiceData.Import(path).Select(x=>x.word).SequenceEqual(words),"export then import must preserve exact words");
 }

 static void TxtBoundaries() {
  string path=FilePath("terms.txt");
  File.WriteAllText(path,"  睡蕉  \r\n\r\nBana\nBana\nbana\n",new UTF8Encoding(true));
  var actual=VoiceData.Import(path);
  Check(actual.Select(x=>x.word).SequenceEqual(new[]{"睡蕉","Bana","bana"}),"trim blanks, exact duplicate, case-sensitive display");
 }

 static void Limits() {
  string longPath=FilePath("long.txt");
  File.WriteAllText(longPath,new string('a',101),new UTF8Encoding(false));
  Throws<InvalidDataException>(()=>VoiceData.Import(longPath),"word length limit");
  string many=FilePath("many.txt");
  File.WriteAllLines(many,Enumerable.Range(0,5001).Select(x=>"term-"+x),new UTF8Encoding(false));
  Throws<InvalidDataException>(()=>VoiceData.Import(many),"term count limit");
  string alias=FilePath("aliases.csv");
  File.WriteAllText(alias,"word,aliases\nword,"+string.Join("|",Enumerable.Range(0,22).Select(x=>" a"+(x%21)+" ")),new UTF8Encoding(false));
  var value=VoiceData.Import(alias).Single();
  Equal(20,value.aliases.Count,"alias limit after trim and distinct");
 }
 static void ImportAndMergeLimits() {
  string oversized=FilePath("oversized.txt");using(var file=new FileStream(oversized,FileMode.Create,FileAccess.Write,FileShare.None)){file.SetLength(4*1024*1024+1);}
  Throws<InvalidDataException>(()=>VoiceData.Import(oversized),"oversized import must be rejected before parsing");
  var current=Enumerable.Range(0,5000).Select(x=>Term("existing-"+x)).ToList();
  Throws<InvalidDataException>(()=>VoiceData.Merge(current,new[]{Term("one-too-many")}),"merge must enforce 5000 terms across existing and incoming batches");
  var oldTerm=Term("BanaStudio",Enumerable.Range(0,15).Select(x=>"alias-"+x));var incoming=Term("BanaStudio",Enumerable.Range(10,15).Select(x=>"alias-"+x));var merged=VoiceData.Merge(new[]{oldTerm},new[]{incoming}).Single();
  Equal(20,merged.aliases.Count,"alias union must remain capped at 20 across merge batches");Check(merged.aliases.SequenceEqual(Enumerable.Range(0,20).Select(x=>"alias-"+x)),"alias merge keeps stable existing order and first new values");
 }

 static void Merge() {
  var original=Term("BanaStudio",new[]{"Banarec","香蕉录屏"},false);
  string created=original.createdAt;
  var incoming=Term("BanaStudio",new[]{"Banarec","香蕉工作室"},true,"import");
  var actual=VoiceData.Merge(new[]{original},new[]{incoming}).Single();
  Equal("BanaStudio",actual.word,"stable word");
  Check(actual.aliases.SequenceEqual(new[]{"Banarec","香蕉录屏","香蕉工作室"}),"alias union without duplicates");
  Check(actual.enabled,"incoming enabled state");
  Equal(created,actual.createdAt,"created timestamp remains stable");
 }

 static void UnsupportedExtension() {
  string path=FilePath("terms.xml");File.WriteAllText(path,"<terms/>");
  Throws<InvalidDataException>(()=>VoiceData.Import(path),"unsupported extension");
 }
}
