using Microsoft.ML;
using Microsoft.ML.Data;
using SixLabors.ImageSharp;


namespace MlModel
{
    public static class BasicSchedule
    {
        public const int TrainingImageWidth = 900;
        public const int TrainingImageHeight = 600;

        #region model input class
        public class ModelInput
        {
            [LoadColumn(0)]
            [ColumnName(@"Labels")]
            public string[]? Labels { get; set; }

            [LoadColumn(1)]
            [ColumnName(@"Image")]
            [Microsoft.ML.Transforms.Image.ImageType(TrainingImageHeight, TrainingImageWidth)]
            public MLImage? Image { get; set; }

            [LoadColumn(2)]
            [ColumnName(@"Box")]
            public float[]? Box { get; set; }
        }

        #endregion

        #region model output class
        public class ModelOutput
        {
            [ColumnName(@"Labels")]
            public uint[]? Labels { get; set; }

            [ColumnName(@"Image")]
            [Microsoft.ML.Transforms.Image.ImageType(TrainingImageHeight, TrainingImageWidth)]
            public MLImage? Image { get; set; }

            [ColumnName(@"Box")]
            public float[]? Box { get; set; }

            [ColumnName(@"PredictedLabel")]
            public string[]? PredictedLabel { get; set; }

            [ColumnName(@"score")]
            public float[]? Score { get; set; }

            [ColumnName(@"PredictedBoundingBoxes")]
            public float[]? PredictedBoundingBoxes { get; set; }

        }

        #endregion

        public class ModelPredictedBox
        {
            public float Score { get; set; }
            public float XTop { get; set; }
            public float XBottom { get; set; }
            public float YTop { get; set; }
            public float YBottom { get; set; }
            public string Label { get; set; }

            private ModelPredictedBox(float xTop, float yTop, float xBottom, float yBottom, string label, float score)
            {
                XTop = xTop;
                XBottom = xBottom;
                YTop = yTop;
                YBottom = yBottom;
                Label = label;
                Score = score;
            }

            public static ModelPredictedBox[] Create(ModelOutput modelOutput)
            {
                var boxes =
                modelOutput.PredictedBoundingBoxes!.Chunk(4)
                    .Select((x, index) => new ModelPredictedBox(x[0], x[1], x[2], x[3], modelOutput.PredictedLabel![index], modelOutput.Score![index]))
                    .ToArray();
                return boxes;
            }

            public static ModelPredictedBox[] CreateWithFilter(ModelOutput modelOutput, double score)
            {
                var boxes =
                modelOutput.PredictedBoundingBoxes!.Chunk(4)
                    .Select((x, index) => new ModelPredictedBox(x[0], x[1], x[2], x[3], modelOutput.PredictedLabel![index], modelOutput.Score![index]))
                    .Where(x => x.Score >= score)
                    .ToArray();
                return boxes;
            }
        }

        private static readonly string MLNetModelPath = Path.GetFullPath(".\\ML_Model\\BasicSchedule.mlnet");

        // 延迟初始化，第一次使用时再初始化
        public static readonly Lazy<PredictionEngine<ModelInput, ModelOutput>> PredictEngine = new Lazy<PredictionEngine<ModelInput, ModelOutput>>(() => CreatePredictEngine(), true);


        private static PredictionEngine<ModelInput, ModelOutput> CreatePredictEngine()
        {
            var mlContext = new MLContext();
            ITransformer mlModel = mlContext.Model.Load(MLNetModelPath, out var _);
            return mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(mlModel);
        }

        /// <summary>
        /// Use this method to predict on <see cref="ModelInput"/>.
        /// </summary>
        /// <param name="input">model input.</param>
        /// <returns><seealso cref=" ModelOutput"/></returns>
        public static ModelOutput Predict(ModelInput input)
        {
            var predEngine = PredictEngine.Value;
            var output = predEngine.Predict(input);

            CalculateAspectAndOffset(input.Image!.Width, input.Image!.Height, TrainingImageWidth, TrainingImageHeight, out float xOffset, out float yOffset, out float aspect);

            if (output.PredictedBoundingBoxes != null && output.PredictedBoundingBoxes.Length > 0)
            {
                for (int x = 0; x < output.PredictedBoundingBoxes.Length; x += 2)
                {
                    output.PredictedBoundingBoxes[x] = (output.PredictedBoundingBoxes[x] - xOffset) / aspect;
                    output.PredictedBoundingBoxes[x + 1] = (output.PredictedBoundingBoxes[x + 1] - yOffset) / aspect;
                }
            }
            return output;
        }

        private static void CalculateAspectAndOffset(float sourceWidth, float sourceHeight, float destinationWidth, float destinationHeight, out float xOffset, out float yOffset, out float aspect)
        {
            float widthAspect = destinationWidth / sourceWidth;
            float heightAspect = destinationHeight / sourceHeight;
            xOffset = 0;
            yOffset = 0;
            if (heightAspect < widthAspect)
            {
                aspect = heightAspect;
                xOffset = (destinationWidth - (sourceWidth * aspect)) / 2;
            }
            else
            {
                aspect = widthAspect;
                yOffset = (destinationHeight - (sourceHeight * aspect)) / 2;
            }
        }
    }
}
