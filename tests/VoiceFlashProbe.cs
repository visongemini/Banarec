using System;
using System.Collections.Generic;
using System.IO;

class VoiceFlashProbe {
 static byte[] ReadPcm(string path) {
  using(var stream=File.OpenRead(path))using(var reader=new BinaryReader(stream)) {
   if(new string(reader.ReadChars(4))!="RIFF"){throw new InvalidDataException("probe is not RIFF WAV");}
   reader.ReadInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("probe is not WAVE");
   bool format=false;short channels=0,bits=0;int rate=0;byte[] pcm=null;
   while(stream.Position+8<=stream.Length){string id=new string(reader.ReadChars(4));int size=reader.ReadInt32();if(size<0||stream.Position+size>stream.Length)throw new InvalidDataException("invalid WAV chunk");long end=stream.Position+size;if(id=="fmt "){short encoding=reader.ReadInt16();channels=reader.ReadInt16();rate=reader.ReadInt32();reader.ReadInt32();reader.ReadInt16();bits=reader.ReadInt16();format=encoding==1;}else if(id=="data")pcm=reader.ReadBytes(size);stream.Position=end+(size&1);if(pcm!=null&&format)break;}
   if(!format||channels!=1||bits!=16||rate!=16000||pcm==null)throw new InvalidDataException("probe must be 16 kHz mono PCM16 WAV");return pcm;
  }
 }
 static int Main(string[] args) {
  if(args.Length<2||args.Length>3){Console.Error.WriteLine("usage: VoiceFlashProbe <wav> <env-file> [--save-dpapi]");return 2;}
  try {
   var options=new VoiceOptions();VoiceFlashCredentials.ApplyFromFile(options,args[1]);
   var client=new FlashAsrClient(options,new List<string>{"琵琶雅礼","睡蕉"});
   string text=client.Recognize(ReadPcm(args[0]),System.Threading.CancellationToken.None).GetAwaiter().GetResult();if(args.Length==3&&args[2]=="--save-dpapi"){options.Backend="flash";VoiceData.SaveOptions(options);}
   Console.WriteLine("backend=flash status=success code=20000000 text="+text);return string.IsNullOrWhiteSpace(text)?1:0;
  } catch(FlashAsrException error){Console.WriteLine("backend=flash status=service-error code="+(string.IsNullOrEmpty(error.StatusCode)?"none":error.StatusCode)+" text=");return 1;}
  catch(Exception){Console.WriteLine("backend=flash status=client-error code=none text=");return 1;}
 }
}
