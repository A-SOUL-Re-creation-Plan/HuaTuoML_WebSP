using System.Text.Json;
using RestSharp;
using CalendarBody;

namespace HuaTuoML_WebSP
{
    public static class Timestamp
    {
        public enum TimestampType
        {
            Seconds,
            MilliSeconds
        }

        public static DateTime TimestampToDate(string timestamp)
        {
            long ts = Convert.ToInt64(timestamp);
            return DateTimeOffset.FromUnixTimeSeconds(ts).DateTime + new TimeSpan(8, 0, 0);
        }
        public static DateTime TimestampToDate(string timestamp, TimestampType type)
        {
            long ts = Convert.ToInt64(timestamp);
            if (type == TimestampType.Seconds)
                return DateTimeOffset.FromUnixTimeSeconds(ts).DateTime + new TimeSpan(8, 0, 0);
            else
                return DateTimeOffset.FromUnixTimeMilliseconds(ts).DateTime + new TimeSpan(8, 0, 0);
        }
        public static DateTime TimestampToDate(long timestamp)
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime + new TimeSpan(8, 0, 0);
        }
        public static DateTime TimestampToDate(long timestamp, TimestampType type)
        {
            if (type == TimestampType.Seconds)
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime + new TimeSpan(8, 0, 0);
            else
                return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime + new TimeSpan(8, 0, 0);
        }

        public static long DateToTimestamp(DateTime date)
        {
            return new DateTimeOffset(date).ToUnixTimeSeconds();
        }
        public static long DateToTimestamp(DateTime date, TimestampType type)
        {
            if (type == TimestampType.Seconds)
                return new DateTimeOffset(date).ToUnixTimeSeconds();
            else
                return new DateTimeOffset(date).ToUnixTimeMilliseconds();
        }

        public static long Tss { get => new DateTimeOffset(DateTime.Now.ToUniversalTime()).ToUnixTimeSeconds(); }
        public static long Tsm { get => new DateTimeOffset(DateTime.Now.ToUniversalTime()).ToUnixTimeMilliseconds(); }
    }

    public static class HttpTools
    {
        public class LowerCaseNamingPolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name) =>
                name.ToLower();
        }

        private static readonly JsonSerializerOptions json_option = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = new LowerCaseNamingPolicy(),
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions deserialize_option = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public static JsonSerializerOptions JsonOption { get => json_option; }
        public static JsonSerializerOptions DeserializeOption { get => deserialize_option; }

        /// <summary>
        /// 确认正常响应，尝试提取信息
        /// </summary>
        /// <param name="resp">响应体</param>
        /// <exception cref="HttpRequestException">Http请求时发生错误</exception>
        /// <exception cref="FeishuException">请求成功，但飞书端抛出错误</exception>
        public static void EnsureSuccessful(RestResponse resp)
        {
            if (!resp.IsSuccessful)
            {
                FeishuErrorResponse errorResponse = JsonSerializer.Deserialize<FeishuErrorResponse>(resp.RawBytes, JsonOption)
                    ?? throw resp.ErrorException ?? new HttpRequestException(resp.StatusDescription, null, resp.StatusCode);
                throw new FeishuException(errorResponse);
            }
        }
    }

    /// <summary>
    /// 飞书反馈的错误内容
    /// </summary>
    public record FeishuErrorResponse
    {
        public required int Code { get; set; }
        public required string Msg { get; set; }
    }

    /// <summary>
    /// 飞书反馈的错误
    /// </summary>
    public sealed class FeishuException : Exception
    {
        public FeishuErrorResponse Response { get; }

        public FeishuException() : base() => this.Response = new FeishuErrorResponse() { Code = -1, Msg = "unknown" };
        public FeishuException(string message) : base(message) => this.Response = new FeishuErrorResponse() { Code = -1, Msg = message };
        public FeishuException(string message, Exception innerException) : base(message, innerException) => this.Response = new FeishuErrorResponse() { Code = -1, Msg = message };
        public FeishuException(FeishuErrorResponse response) => this.Response = response;

        public override string Message { get => this.Response.Msg; }
    }

    /// <summary>
    /// 飞书ID
    /// </summary>
    public sealed class LarkID
    {
        public readonly string id;
        public readonly string id_type;

        public override string ToString() => id;

        public LarkID(string id)
        {
            this.id = id;
            if (id.StartsWith("ou"))
                this.id_type = "open_id";
            else if (id.StartsWith("oc"))
                this.id_type = "chat_id";
            else if (id.StartsWith("on"))
                this.id_type = "union_id";
            else if (id.StartsWith("cli_"))
                this.id_type = "app_id";
            else
                throw new NotSupportedException();
        }
        public LarkID(string id, string id_type)
        {
            this.id = id;
            this.id_type = id_type;
        }
    }

    public class FeishuUserToken(string token)
    {
        public string UserToken { get; } = token;
        // public bool Is_outdate { get => (expire - Timestamp.Tss) < 0; }
    }

    public class FeishuCLDService(string calendar_id, FeishuUserToken userToken)
    {
        private string calendar_id = calendar_id;
        private FeishuUserToken token = userToken;

        private static readonly Uri _base_uri = new("https://open.feishu.cn/open-apis/calendar/v4/calendars/");
        private readonly RestClient _client = new RestClient();

        /// <summary>
        /// 获取日程信息
        /// </summary>
        /// <param name="event_id">日程ID</param>
        /// <returns>CalendarBody.GetEventResponse</returns>
        /// <exception cref="Exception">反序列化失败</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task<GetEventResponse> GetEvent(string event_id)
        {
            var request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/{event_id}");

            request.AddHeader("Authorization", $"Bearer {token.UserToken}");

            var resp = await _client.ExecuteAsync(request, Method.Get);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<GetEventResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        /// <summary>
        /// 获取日程列表
        /// </summary>
        /// <param name="start_time">开始时间（戳）</param>
        /// <param name="end_time">结束时间（戳）</param>
        /// <param name="page_size">页大小</param>
        /// <param name="page_token"></param>
        /// <param name="sync_token"></param>
        /// <returns>CalendarBody.GetEventListResponse</returns>
        /// <exception cref="Exception">反序列化失败</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task<GetEventListResponse> GetEventList(string? start_time = null, string? end_time = null,
            int? page_size = null, string? page_token = null, string? sync_token = null, bool ignore_cancelled = true)
        {
            RestRequest request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/");
            if (start_time != null) request.AddQueryParameter("start_time", start_time);
            if (end_time != null) request.AddQueryParameter("end_time", end_time);
            if (page_size != null) request.AddQueryParameter("page_size", page_size.ToString());
            if (page_token != null) request.AddQueryParameter("page_token", page_token);
            if (sync_token != null) request.AddQueryParameter("sync_token", sync_token);

            request.AddHeader("Authorization", $"Bearer {token.UserToken}");

            var resp = await _client.ExecuteAsync(request, Method.Get);
            HttpTools.EnsureSuccessful(resp);
            var json_obj = JsonSerializer.Deserialize<GetEventListResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
            if (ignore_cancelled)
            {
                List<CalendarEvent> event_list = json_obj.Data.Items.ToList();
                List<CalendarEvent> editable_list = json_obj.Data.Items.ToList();
                foreach (var event_obj in event_list)
                    if (event_obj.Status == "cancelled") editable_list.Remove(event_obj);
                json_obj.Data.Items = editable_list.ToArray();
            }
            return json_obj;
        }

        /// <summary>
        /// 创建日程
        /// </summary>
        /// <param name="calendarEvent">要创建的日程</param>
        /// <returns>CalendarBody.GetEventResponse</returns>
        /// <exception cref="Exception">反序列化失败</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task<GetEventResponse> CreateEvent(CalendarEventEditable calendarEvent)
        {
            var request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/");

            request.AddBody(calendarEvent);

            request.AddHeader("Authorization", $"Bearer {token.UserToken}");

            var resp = await _client.ExecuteAsync(request, Method.Post);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<GetEventResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        /// <summary>
        /// 删除日程
        /// </summary>
        /// <param name="event_id">日程ID</param>
        /// <returns></returns>
        /// <exception cref="Exception">反序列化失败</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task DeleteEvent(string event_id)
        {
            var request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/{event_id}");

            request.AddQueryParameter("need_notification", "false");

            request.AddHeader("Authorization", $"Bearer {token.UserToken}");

            var resp = await _client.ExecuteAsync(request, Method.Delete);
            HttpTools.EnsureSuccessful(resp);
        }

        /// <summary>
        /// 编辑日程
        /// </summary>
        /// <param name="event_id">日程ID</param>
        /// <param name="calendarEvent">要更新的日程</param>
        /// <returns></returns>
        /// <exception cref="Exception">反序列化失败</exception>
        /// <exception cref="FeishuException">飞书端抛出错误</exception>
        /// <exception cref="HttpRequestException">Http请求时抛出错误</exception>
        public async Task<GetEventResponse> EditEvent(string event_id, CalendarEventEditable calendarEvent)
        {
            var request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/{event_id}");

            request.AddBody(calendarEvent);

            request.AddHeader("Authorization", $"Bearer {token.UserToken}");

            var resp = await _client.ExecuteAsync(request, Method.Patch);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<GetEventResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }

        public async Task AddAttendees(string event_id, LarkID[] lark_id)
        {
            var request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/{event_id}/attendees");

            List<object> attendees_list = new List<object>();
            foreach (var person in lark_id)
            {
                if (person.id_type == "open_id")
                {
                    attendees_list.Add(new
                    {
                        type = "user",
                        user_id = person.id
                    });
                }
                else if (person.id_type == "chat_id")
                {
                    attendees_list.Add(new
                    {
                        type = "chat",
                        chat_id = person.id
                    });
                }
                else throw new NotSupportedException();
            }

            request.AddBody(new
            {
                attendees = attendees_list,
                need_notification = false
            });

            request.AddHeader("Authorization", $"Bearer {token.UserToken}");

            var resp = await _client.ExecuteAsync(request, Method.Post);
            HttpTools.EnsureSuccessful(resp);
        }

        public async Task DeleteAttendeesUser(string event_id, string[] attendee_ids)
        {
            var request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/{event_id}/attendees/batch_delete");

            request.AddBody(new
            {
                attendee_ids,
                need_notification = false
            });

            request.AddHeader("Authorization", $"Bearer {token.UserToken}");

            var resp = await _client.ExecuteAsync(request, Method.Post);
            HttpTools.EnsureSuccessful(resp);
        }

        public async Task<GetAttendeesResponse> GetAttendeesList(string event_id)
        {
            var request = new RestRequest($"{_base_uri.OriginalString}{calendar_id}/events/{event_id}/attendees");

            request.AddHeader("Authorization", $"Bearer {token.UserToken}");

            var resp = await _client.ExecuteAsync(request, Method.Get);
            HttpTools.EnsureSuccessful(resp);
            return JsonSerializer.Deserialize<GetAttendeesResponse>(resp.RawBytes, HttpTools.JsonOption) ??
                throw new Exception("Deserialize Failed");
        }
    }

    public record CalendarEventEditable
    {
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public StartTime? Start_time { get; set; }
        public EndTime? End_time { get; set; }
        public Vchat? Vchat { get; set; }
        public string? Visibility { get; set; }
        public string? Attendee_ability { get; set; }
        public int? Color { get; set; }
        public Reminder[]? Reminders { get; set; }
    }
}

namespace CalendarBody
{

    public record GetEventResponse
    {
        public required int Code { get; set; }
        public required string Msg { get; set; }
        public required GetData Data { get; set; }
    }

    public record GetEventListResponse
    {
        public required int Code { get; set; }
        public required string Msg { get; set; }
        public required GetListData Data { get; set; }
    }

    public record GetData
    {
        public required CalendarEvent Event { get; set; }
    }

    public record GetListData
    {
        public bool Has_more { get; set; }
        public required string Page_token { get; set; }
        public required string Sync_token { get; set; }
        public required CalendarEvent[] Items { get; set; }
    }

    public record CalendarEvent
    {
        public required string Event_id { get; set; }
        public string? Organizer_calendar_id { get; set; }
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public required StartTime Start_time { get; set; }
        public required EndTime End_time { get; set; }
        public Vchat? Vchat { get; set; }
        public string? Visibility { get; set; }
        public string? Attendee_ability { get; set; }
        public required int Color { get; set; }
        public Reminder[]? Reminders { get; set; }
        public required string Status { get; set; }
        public required string Create_time { get; set; }
    }

    public record StartTime
    {
        public required string Timestamp { get; set; }
    }

    public record EndTime
    {
        public required string Timestamp { get; set; }
    }

    public record Vchat
    {
        public required string Vc_type { get; set; }
    }

    public record Reminder
    {
        public int Minutes { get; set; }
    }



    public class GetAttendeesResponse
    {
        public required int Code { get; set; }
        public required string Msg { get; set; }
        public required AttendeesData Data { get; set; }
    }

    public class AttendeesData
    {
        public required AttendeesItem[] Items { get; set; }
        public required bool Has_more { get; set; }
        public string? Page_token { get; set; }
    }

    public class AttendeesItem
    {
        public required string Type { get; set; }
        public required string Attendee_id { get; set; }
        public required string Display_name { get; set; }
        public string? User_id { get; set; }
        public string? Chat_id { get; set; }
    }
}
