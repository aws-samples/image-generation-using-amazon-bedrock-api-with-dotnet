using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Constructs;
using System;
using System.Collections.Generic;

namespace BedrockImageGenerationCdk
{
    public class BedrockImageGenerationCdkStack : Stack
    {
        public BedrockImageGenerationCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            string environment = "DEV";

            var s3Bucket = new Bucket(this, "GenAIImages", new BucketProps
            {
                BucketName = (environment + "-GenAIImages" + Account).ToLower(),
                RemovalPolicy = RemovalPolicy.RETAIN
            });

            var stableDiffusionXLG1Handler = new Function(this, "StableDiffusionXLG1Handler", new FunctionProps
            {
                Runtime = new Runtime("dotnet8", RuntimeFamily.DOTNET_CORE), // To support DOTNET_8 runtime https://github.com/aws/aws-lambda-dotnet/issues/1611,
                FunctionName = "StableDiffusionXLG1Handler",
                //Where to get the code
                Code = Code.FromAsset(".\\src\\TextToImageLambdaFunction\\bin\\Debug\\net8.0"),
                Handler = "TextToImageLambdaFunction::TextToImageLambdaFunction.Function::StableDiffusionXLG1Handler",
                Environment = new Dictionary<string, string>
                {
                    ["ENVIRONMENT"] = environment,
                    ["BUCKET"] = s3Bucket.BucketName
                },
                Timeout = Duration.Seconds(900)
            });

            // Assign permissions to Lambda to invoke Bedrock model
            string[] actions = {
                "bedrock:InvokeModel"
            };

            var policy = new PolicyStatement(new PolicyStatementProps()
            {
                Sid = "BedrockPermissionForLambda",
                Actions = actions,
                Effect = Effect.ALLOW,
                Resources = new string[] { "*" }
            });
            
            stableDiffusionXLG1Handler.AddToRolePolicy(policy);
            
            // assign put permission to stableDiffusionXLG1Handler lambda to write an image to S3 bucket and 
            // read permission so that generated presigned URL should not give access denied error
            s3Bucket.GrantReadWrite(stableDiffusionXLG1Handler);

            // create API in API Gateway
            var restAPI = new RestApi(this, "BedrockImageGenerationRestAPI", new RestApiProps
            {
                RestApiName = "BedrockImageGenerationRestAPI",
                Description = "This API provide endponts to interact with Bedrock for text eneration",
                Deploy = false
            });

            var deployment = new Deployment(this, "My Deployment", new DeploymentProps { Api = restAPI });
            var stage = new Amazon.CDK.AWS.APIGateway.Stage(this, "stage name", new Amazon.CDK.AWS.APIGateway.StageProps
            {
                Deployment = deployment,
                StageName = environment,
                // enable tracing x-ray
                TracingEnabled = true,
            });

            restAPI.DeploymentStage = stage;

            var imageResource = restAPI.Root.AddResource("image");
            imageResource.AddMethod("POST", new LambdaIntegration(stableDiffusionXLG1Handler, new LambdaIntegrationOptions()
            {
                Proxy = true
            }));

            //Output results of the CDK Deployment
            new CfnOutput(this, "Deployment start Time:", new CfnOutputProps() { Value = DateTime.Now.ToString() });
            new CfnOutput(this, "Region:", new CfnOutputProps() { Value = this.Region });
            new CfnOutput(this, "Amazon API Gateway Enpoint:", new CfnOutputProps() { Value = restAPI.Url });
            new CfnOutput(this, "Amazon S3 Bucket Name:", new CfnOutputProps() { Value = s3Bucket.BucketName });
        }
    }
}
