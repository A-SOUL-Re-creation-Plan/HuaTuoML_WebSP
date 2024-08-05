using System.Text.Json;
using HuaTuo.Service;
using HuaTuoMain.CloudServe;
using Microsoft.AspNetCore.Mvc;
using RestSharp;


namespace HuaTuoML_WebSP
{
    public record ScheduleTask
    {
        public string UUID { get; } = Guid.NewGuid().ToString();
        public List<string> Messages { get; } = new List<string>();
        public string Status { get; set; } = "waiting";
        public string Output { get; set; } = "";
    }

    public class Program
    {
        public static readonly Dictionary<string, ScheduleTask> TaskQueue = new Dictionary<string, ScheduleTask>();

        public static void Main(string[] args)
        {
            FeishuConfig feishu_cfg = JsonSerializer.Deserialize<FeishuConfig>(File.ReadAllText("./config.json")) ?? 
                throw new Exception("未发现config文件");

            ServiceOCR ocr = new ServiceOCR(feishu_cfg.Tencent_SecretID, feishu_cfg.Tencent_SecretKey);

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddAuthorization();

            var app = builder.Build();
            app.UseAuthorization();

            app.MapPost("/create", (HttpContext httpContext) =>
            {
                ScheduleTask task = new ScheduleTask();
                try
                {
                    string path = httpContext.Request.Query["path"].ToString();
                    // 识别是否为网页链接
                    var image = new MemoryStream();
                    if (Uri.IsWellFormedUriString(path, UriKind.Absolute))
                    {
                        var client = new RestClient();
                        var req = new RestRequest(path);
                        req.AddHeader("User-Agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/63.0.3239.108");
                        image = new MemoryStream(client.Get(req).RawBytes!);
                    }
                    else
                    {
                        image = new MemoryStream(File.ReadAllBytes(path));
                    }
                    string calendar_id = httpContext.Request.Query["calendar"].ToString();
                    string user_token = httpContext.Request.Query["token"].ToString();
                    var token = new FeishuUserToken(user_token);
                    TaskQueue.Add(task.UUID, task);
                    new Thread(() =>
                    {
                        try
                        {
                            var schedule_process = new ScheduleProcess(image, ref task, new FeishuCLDService(calendar_id, token), feishu_cfg, ocr);
                            schedule_process.StartParsing().Wait();
                            task.Status = "success";
                        }
                        catch (Exception e)
                        {
                            task.Status = "error";
                            task.Messages.Add(e.ToString());
                        }
                        
                    }).Start();
                }
                catch (Exception e)
                {
                    return Results.BadRequest(new
                    {
                        uuid = task.UUID,
                        msg = e.Message
                    });
                }
                
                return Results.Ok(new
                {
                    uuid = task.UUID,
                    msg = "success"
                });
            });

            app.MapGet("/status", (HttpContext httpContext) =>
            {
                var uuid = httpContext.Request.Query["uuid"].ToString();
                if (!TaskQueue.ContainsKey(uuid)) return Results.NotFound();
                var task = TaskQueue[uuid];
                if (task == null) return Results.NotFound();
                lock (task)
                {
                    return Results.Ok(new
                    {
                        uuid = task.UUID,
                        status = task.Status,
                        messages = task.Messages,
                        output = task.Output
                    });
                }
            });

            app.MapGet("/output", (HttpContext httpContext) =>
            {
                var uuid = httpContext.Request.Query["uuid"].ToString();
                if (!TaskQueue.ContainsKey(uuid)) return Results.NotFound();
                var task = TaskQueue[uuid];
                try
                {
                    return Results.Ok(new FileContentResult(File.ReadAllBytes(task.Output), "image/jpeg"));
                }
                catch (Exception e)
                {
                    return Results.NotFound(new { msg = e.Message});
                }
            });

            app.Run();
        }
    }

    public record FeishuConfig
    {
        public required string FeishuID_Ava { get; set; }
        public required string FeishuID_Bella { get; set; }
        public required string FeishuID_Diana { get; set; }
        public required string FeishuID_Eileen { get; set; }
        public required string FeishuID_Jihua { get; set; }
        public required string FeishuID_ASOUL { get; set; }

        public required string Tencent_SecretID { get; set; }
        public required string Tencent_SecretKey { get; set; }
    }
}
