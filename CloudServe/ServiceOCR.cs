using MlModel;
using TencentCloud.Common;
using TencentCloud.Ocr.V20181119;
using TencentCloud.Ocr.V20181119.Models;

namespace HuaTuoMain.CloudServe
{
    public class ServiceOCR
    {
        private OcrClient ocrClient;

        public ServiceOCR(string SecretID, string SecretKey)
        {
            Credential crd = new Credential()
            {
                SecretId = SecretID,
                SecretKey = SecretKey
            };
            ocrClient = new OcrClient(crd, "ap-guangzhou");
        }

        public async Task<GeneralAccurateOCRResponse> RequestAsync(string img_url)
        {
            GeneralAccurateOCRRequest request = new GeneralAccurateOCRRequest();
            request.ImageUrl = img_url;
            return await ocrClient.GeneralAccurateOCR(request);
        }

        public async Task<GeneralAccurateOCRResponse> RequestAsync(byte[] img_bytes)
        {
            GeneralAccurateOCRRequest request = new GeneralAccurateOCRRequest();
            request.ImageBase64 = Convert.ToBase64String(img_bytes);
            return await ocrClient.GeneralAccurateOCR(request);
        }
    }

    public class OcrTools(GeneralAccurateOCRResponse resp)
    {
        public GeneralAccurateOCRResponse response = resp;

        public TextDetection[] SearchPoint(int x, int y)
        {
            List<TextDetection> blocks = new List<TextDetection>();
            foreach (var block in response.TextDetections)
            {
                var dis_x = x - block.ItemPolygon.X;
                var dis_y = y - block.ItemPolygon.Y;
                if (dis_x >= 0 && dis_x <= block.ItemPolygon.Width)
                    if (dis_y >= 0 && dis_y <= block.ItemPolygon.Height)
                        blocks.Add(block);
            }
            return blocks.ToArray();
        }

        public TextDetection[] SearchRectangle(int x, int y, int width, int height)
        {
            List<TextDetection> blocks = new List<TextDetection>();
            foreach (var block in response.TextDetections)
            {
                var arg_x = block.ItemPolygon.X + (block.ItemPolygon.Width / 2);
                var arg_y = block.ItemPolygon.Y + (block.ItemPolygon.Height / 2);
                var dis_x = arg_x - x;
                var dis_y = arg_y - y;
                if (dis_x >= 0 && dis_x <= width)
                    if (dis_y >= 0 && dis_y <= height)
                        blocks.Add(block);
            }
            return blocks.ToArray();
        }
    }

    public static class GeometryExtension
    {
        public static bool Is_chinese(char[] text)
        {
            foreach (var ch in text)
            {
                if ('\u4e00' <= ch && ch <= '\u9fff')
                    return true;
            }
            return false;
        }

        public static double Distance(this BasicSchedule.ModelPredictedBox box, BasicSchedule.ModelPredictedBox box1)
        {
            double arg1_x = (box.XTop + box.XBottom) / 2;
            double arg1_y = (box.YTop + box.YBottom) / 2;
            double arg2_x = (box1.XTop + box1.XBottom) / 2;
            double arg2_y = (box1.YTop + box1.YBottom) / 2;
            return Math.Sqrt(Math.Pow(arg1_x - arg2_x, 2.0) + Math.Pow(arg1_y - arg2_y, 2.0));
        }

        public static DateTime SearchTimeBase(this OcrTools tools)
        {
            TextDetection[] detections = tools.SearchRectangle(1030, 430, 300, 80);
            foreach (TextDetection detection in detections)
            {
                if (Is_chinese(detection.DetectedText.ToCharArray()))
                    continue;
                if (DateTime.TryParseExact(detection.DetectedText, "MM.dd", null, System.Globalization.DateTimeStyles.None, out var date))
                    return new DateTime(DateTime.Now.Year, date.Month, date.Day);
                if (DateTime.TryParseExact(detection.DetectedText, "M.d", null, System.Globalization.DateTimeStyles.None, out var sdate))
                    return new DateTime(DateTime.Now.Year, sdate.Month, sdate.Day);
            }
            throw new Exception("无法找到日程表时基");
        }

        public static BasicSchedule.ModelPredictedBox[] DetectionsRanging(this BasicSchedule.ModelPredictedBox box, 
                                                                          BasicSchedule.ModelPredictedBox[] boxes, 
                                                                          double range = 420.0)
        {
            var box_list = new List<BasicSchedule.ModelPredictedBox>();
            foreach (var box1 in boxes)
            {
                if (box.Distance(box1) < range)
                    box_list.Add(box1);
            }
            return box_list.ToArray();
        }

        public static int CalcuOffset(BasicSchedule.ModelPredictedBox box)
        {
            int arg_x = ((int)box.XTop + (int)box.XBottom) / 2;
            int arg_y = ((int)box.YTop + (int)box.YBottom) / 2;
            int offset = (arg_x - 923) / 493;
            // 这一步保证offset的符号性可以被体现
            offset++;
            if (arg_y < 1290)
                return offset;
            else return -offset;
        }
    }
}
