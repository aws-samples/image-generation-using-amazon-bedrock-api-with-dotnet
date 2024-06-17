using System.Text.Json.Nodes;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Util;
using System.Net;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TextToImageLambdaFunction;

public class Function
{
    private const string StableDiffusionXLModelId = "stability.stable-diffusion-xl-v1";
    private const double TimeoutDuration = 12;
    private static readonly RegionEndpoint Region = RegionEndpoint.USEast1;
    private static readonly AmazonBedrockRuntimeClient BedrockClient = new(Region);
    private static readonly IAmazonS3 S3Client = new AmazonS3Client(Region);
    private static readonly string BucketName = Environment.GetEnvironmentVariable("BUCKET") ?? throw new InvalidOperationException("BUCKET environment variable is not set.");

    public async Task<APIGatewayProxyResponse> StableDiffusionXLG1Handler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var requestBody = JsonNode.Parse(request.Body);
        var prompt = requestBody!["prompt"]!.ToString();
        var stylePreset = requestBody["stylePreset"]?.ToString();
        var seed = Convert.ToInt32(requestBody["seed"]?.ToString());

        // it will prepare the InvokeModelRequest payload, which will be used by Bedrock client to invoke the model. 
        var payload = PrepareRequestPayload(prompt, stylePreset, seed);

        try
        {
            // invoke the model
            var response = await BedrockClient.InvokeModelAsync(new InvokeModelRequest
            {
                ModelId = StableDiffusionXLModelId,
                Body = AWSSDKUtils.GenerateMemoryStreamFromString(payload),
                ContentType = "application/json",
                Accept = "application/json"
            });

            if (response.HttpStatusCode == HttpStatusCode.OK)
            {
                var results = JsonNode.Parse(response.Body)?["artifacts"]?.AsArray();
                var base64String = results?[0]?["base64"]?.GetValue<string>();
                if (string.IsNullOrEmpty(base64String))
                {
                    Console.WriteLine($"No Image generated");
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.InternalServerError,
                        Body = null
                    };
                }
                // here you can write your logic to upload this base64 image to S3 and
                // return response which contains S3 Presigned URL for uploaded object
                var urlString = await UploadImageToS3AndReturnPreSignedURL(base64String);

                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = urlString
                };

            }
            else
            {
                Console.WriteLine($"InvokeModelAsync failed with status code {response.HttpStatusCode}");
            }
        }
        catch (AmazonBedrockRuntimeException ex)
        {
            Console.WriteLine(ex.Message);
        }

        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.InternalServerError,
            Body = "Internal Server Error"
        };
    }

    private static async Task<string> UploadImageToS3AndReturnPreSignedURL(string base64String)
    {
        var fileName = $"foo-{new Random().Next(100)}.jpg";
        var objectKey = $"{BucketName}/{fileName}";
        var urlString = GeneratePresignedURL(objectKey, TimeoutDuration);

        Console.WriteLine($"The generated URL is: {urlString}.");
        var bytes = Convert.FromBase64String(base64String);
        await UploadImageToS3Bucket(objectKey, bytes);
        return urlString;
    }

    /// <summary>
    /// It prepares a request payload with inference parameters
    /// </summary>
    /// <param name="prompt"></param>
    /// <param name="stylePreset"></param>
    /// <param name="seed"></param>
    /// <returns></returns>
    private static string PrepareRequestPayload(string prompt, string? stylePreset, int seed)
    {
        var jsonPayload = new JsonObject
        {
            { "text_prompts", new JsonArray
                {
                    new JsonObject
                    {
                        { "text", prompt }
                    }
                }
            },
            { "seed", seed }
        };

        if (!string.IsNullOrEmpty(stylePreset))
        {
            jsonPayload.Add("style_preset", stylePreset);
        }

        return jsonPayload.ToString();
    }

    private static async Task UploadImageToS3Bucket(string objectKey, byte[] bytes)
    {
        Console.WriteLine("Starting the S3 put request.");
        var putRequest = new PutObjectRequest
        {
            BucketName = BucketName,
            Key = objectKey,
        };

        using var ms = new MemoryStream(bytes);
        putRequest.InputStream = ms;
        await S3Client.PutObjectAsync(putRequest);
        Console.WriteLine("Completing the S3 put request.");
    }

    private static string GeneratePresignedURL(string objectKey, double duration)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = BucketName,
                Key = objectKey,
                Expires = DateTime.UtcNow.AddHours(duration)
            };
            return S3Client.GetPreSignedURL(request);
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error: '{ex.Message}'");
            return string.Empty;
        }
    }
}

