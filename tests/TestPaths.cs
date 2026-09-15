using System;
using System.IO;
static class TestPaths {
 public static string Artifacts {
  get {
   string path=Environment.GetEnvironmentVariable("BANAREC_TEST_OUTPUT");
   if(String.IsNullOrWhiteSpace(path))path=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","tests","artifacts"));
   Directory.CreateDirectory(path);return path;
  }
 }
}
