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
        private delegate void OnProgressNotify(ulong success, ulong fail, ulong total);

        [DllImport("pcileech.dll")]
        static extern int StartDump(OnProgressNotify opn,string outPath);

        [DllImport("pcileech.dll")]
        static extern void StopDump();

        static int _status = 0;

        static int _stopStatus = 0;

        public async Task<IActionResult> DumpMemory()
        {
            var dic=new Dictionary<string,object>();
            var status = 0;//0 ok 1 failed 2 running
            if(Interlocked.CompareExchange(ref _status, 1, 0) == 0)
            {
                try
                {
                    Response.ContentType = "text/event-stream; charset=utf-8";
                    Response.Headers["Cache-Control"] = "no-cache";
                    await Response.StartAsync();

                    var sw = new StreamWriter(Response.BodyWriter.AsStream(), Encoding.UTF8);
                    var opn = new OnProgressNotify((ulong success, ulong fail, ulong total) => {
                        dic["success"] = success / 256;
                        dic["fail"] = fail / 256;
                        dic["total"] = total / 256;
                        dic["progress"] = (success + fail) * 100.0 / total;
                        sw.WriteLine(JsonConvert.SerializeObject(dic));
                        sw.Flush();
                    });
                    var name = DateTime.Now.ToString("yyyyMMddHHmmss");
                    if (Request.Query.ContainsKey("name"))
                    {
                        name += Request.Query["name"];
                    }
                    name += ".raw";
                    status = StartDump(opn, "mem/" + name);

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
