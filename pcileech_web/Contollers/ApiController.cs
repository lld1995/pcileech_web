using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Text;

namespace pcileech_web.Contollers
{
    [Route("Api/[action]")]
    [ApiController]
    public class ApiController : Controller
    {
        private delegate void OnProgressNotify(ulong success, ulong fail, ulong total,ulong speed);

        [DllImport("pcileech.dll")]
        static extern int StartDump(OnProgressNotify opn,string outPath,int only700,ulong pmax);

        [DllImport("pcileech.dll")]
        static extern void StopDump();

        static int _status = 0;

        static int _stopStatus = 0;

        public async Task<IActionResult> DumpMemory()
        {
            ulong pmax = 0;
            if (Request.Query.ContainsKey("pmax"))
            {
                var pmaxStr = Request.Query["pmax"].ToString();
                if (pmaxStr.ToLower().StartsWith("0x"))
                {
                    ulong.TryParse(pmaxStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out pmax);
                }
                else
                {
                    ulong.TryParse(pmaxStr, out pmax);
                }
            }
            int only700 = 0;
            if (Request.Query.ContainsKey("only700"))
            {
                var only700Str = Request.Query["only700"].ToString();
                int.TryParse(only700Str, out only700);
            }

            var dic=new Dictionary<string,object>();
            var status = 0;//0 ok 1 failed 2 running
            if(Interlocked.CompareExchange(ref _status, 1, 0) == 0)
            {
                try
                {

                    int maxRootLength = 0;
                    DriveInfo[] drives = DriveInfo.GetDrives();
                    DriveInfo selectDrive = null;
                    foreach (var drive in drives)
                    {
                        if (drive.RootDirectory.Name.Length > maxRootLength && Environment.CurrentDirectory.StartsWith(drive.RootDirectory.Name))
                        {
                            maxRootLength = drive.RootDirectory.Name.Length;
                            selectDrive = drive;
                        }
                    }
                    if (selectDrive == null)
                    {

                    }
                    else
                    {
                        var availableFreeSpace = selectDrive.AvailableFreeSpace;
                        var files = Directory.GetFiles("mem/");
                        for (int i = 0; i < files.Length; i++)
                        {
                            var surplusRate = availableFreeSpace / (double)selectDrive.TotalSize;
                            Console.WriteLine("surplusRate:" + surplusRate);
                            if (surplusRate < 0.1)
                            {
                                try
                                {
                                    var fi = new FileInfo(files[i]);
                                    var fiLen= fi.Length;
                                    System.IO.File.Delete(files[i]);
                                    availableFreeSpace += fiLen;
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    Response.ContentType = "text/event-stream; charset=utf-8";
                    Response.Headers["Cache-Control"] = "no-cache";
                    await Response.StartAsync();

                    var sw = new StreamWriter(Response.BodyWriter.AsStream(), Encoding.UTF8);
                    var opn = new OnProgressNotify((ulong success, ulong fail, ulong total, ulong speed) => {
                        dic["success"] = success / 256;
                        dic["fail"] = fail / 256;
                        dic["total"] = total / 256;
                        dic["progress"] = (success + fail) * 100.0 / total;
                        if (speed >= 2048)
                        {
                            dic["speed"] = (speed>>10) + " MB/S";
                        }
                        else
                        {
                            dic["speed"] = speed+" KB/S";
                        }
                        sw.WriteLine(JsonConvert.SerializeObject(dic));
                        sw.Flush();
                    });
                    var name = DateTime.Now.ToString("yyyyMMddHHmmss");
                    if (Request.Query.ContainsKey("name"))
                    {
                        name += Request.Query["name"];
                    }
                    name += ".raw";
                    status = StartDump(opn, "mem/" + name, only700, pmax);

                    dic["status"] = status;
                    sw.WriteLine(JsonConvert.SerializeObject(dic));
                    sw.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                _status = 0;
                return new EmptyResult();
            }
            else
            {
                status = 2;
                dic["status"] = status;
                return Content(JsonConvert.SerializeObject(dic));
            }
        }

        public IActionResult ListMem()
        {
            var files = Directory.GetFiles("mem/","*.*",SearchOption.AllDirectories).Reverse();
            return Content(JsonConvert.SerializeObject(files));
        }


        public IActionResult DelMem()
        {
            var dic = new Dictionary<string, object>();
            if (Request.Query.ContainsKey("path"))
            {
                var path = Request.Query["path"].ToString();
                if (path.StartsWith("mem/"))
                {
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                        dic["status"] = 0;
                    }
                    else
                    {
                        dic["status"] = 3;//not exist
                    }
                }
                else
                {
                    dic["status"] = 2;//illegal
                }
            }
            else
            {
                dic["status"] = 1;//arg error
            }
            return Content(JsonConvert.SerializeObject(dic));
        }

        public IActionResult StopDumpMemory()
        {
            var dic = new Dictionary<string, object>();
            if (Interlocked.CompareExchange(ref _stopStatus, 1, 0) == 0)
            {
                try
                {
                    while (_status == 1)
                    {
                        StopDump();
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                _stopStatus = 0;
                dic["status"] = 0;
            }
            else
            {
                dic["status"] = 1;
            }
            return Content(JsonConvert.SerializeObject(dic));
        }
    }
}
