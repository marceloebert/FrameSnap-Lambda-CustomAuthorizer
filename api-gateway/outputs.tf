output "api_gateway_url" {
  description = "Invoke URL for the API Gateway"
  value       = aws_api_gateway_stage.prod_stage.invoke_url
}

output "custom_authorizer_arn" {
  description = "ARN of the Custom Authorizer Lambda"
  value       = data.aws_lambda_function.custom_authorizer.arn
}
