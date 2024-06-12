## Introduction
This sample application is designed to demonstrate how to generate images using Stable diffusion XL model with Amazon Bedrock APIs using dotnet. 

## Deployment

1. Set enviornment variables `CDK_DEFAULT_ACCOUNT` by your AWS account ID and `CDK_DEFAULT_REGION`
2. Install aws-cdk [https://docs.aws.amazon.com/cdk/v2/guide/cli.html]
3. Build the solution
3. Open command prompt and run below commands\
    `dotnet build`\
    `cdk synth`\
    `cdk deploy`

## Testing
You can test by invoking the API endpoint from any client like Postman. Make sure to replace your API endpoint URL, headers, and payload data and method as “POST”.

`https://{api-id}.execute-api.region.amazonaws.com/DEV/image`

#### Request Body:
```
{
"prompt":"An image for girls having fun with with beautiful colors, nice mountain view, with a river and some drinks",
"seed":0,
"stylePreset":"cinematic"
}
```


Once you get the successful response which contains a S3 pre-signed URL, copy that and open in the browser. You should be able to see your image.

## Cleanup
To save costs, delete the resources you created as part of this sample. Use command below

`cdk destroy`

*Note: S3 bucket used in this solution might not empty while using the cdk destroy command for clean-up, so you need to ensure that the buckets are empty before running destroy command.
You can empty the bucket using AWS Console or alternatively using AWS CLI.*

`aws s3 rm s3://{Amazon S3 Bucket Name} —recursive`

You can find Amazon S3 bucket name from cdk deploy command output.

## License

This library is licensed under the MIT-0 License. See the LICENSE file.
