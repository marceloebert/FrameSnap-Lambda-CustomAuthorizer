variable "region" {
  description = "AWS region"
  default     = "us-east-1"
}

variable "eks_service_endpoint" {
  description = "EKS public endpoint (sem /videos no final)"
  default     = "http://a65f7d678ccd0469ba97728784513106-1518254404.us-east-1.elb.amazonaws.com/videos"
}

variable "login_service_endpoint" {
  description = "EKS login service endpoint (rota /login)"
  default     = "http://aac02e3ca3283499a9a6f48fa119442d-1681479322.us-east-1.elb.amazonaws.com/auth"
}

variable "lambda_function_name" {
  description = "Custom Authorizer Lambda function name"
  default     = "CustomAuthorizer"
}

variable "account_id" {
  description = "AWS account ID"
  default     = "339713138979"
}
