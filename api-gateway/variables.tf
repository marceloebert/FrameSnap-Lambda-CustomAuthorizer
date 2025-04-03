variable "region" {
  description = "AWS region"
  default     = "us-east-1"
}

variable "eks_service_endpoint" {
  description = "EKS public endpoint (sem /videos no final)"
  default     = "http://a11eaeb612d6d46deafa7ded6cae33b4-1888218398.us-east-1.elb.amazonaws.com/videos"
}

variable "login_service_endpoint" {
  description = "EKS login service endpoint (rota /login)"
  default     = "http://a0c12299f05334b8d9c54094e1353831-1171296163.us-east-1.elb.amazonaws.com/login"
}

variable "lambda_function_name" {
  description = "Custom Authorizer Lambda function name"
  default     = "CustomAuthorizer"
}

variable "account_id" {
  description = "AWS account ID"
  default     = "735083653075"
}
