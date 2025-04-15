variable "region" {
  description = "AWS region"
  default     = "us-east-1"
}

variable "eks_service_endpoint" {
  description = "EKS public endpoint (sem /videos no final)"
  default     = "http://a8e3cd4e5f0b349dd8d4c43539afbca8-39016098.us-east-1.elb.amazonaws.com/videos"
}

variable "login_service_endpoint" {
  description = "EKS login service endpoint (rota /login)"
  default     = "http://a8ab722c3555440bea881a4fa65463d5-1866891439.us-east-1.elb.amazonaws.com/auth"
}

variable "lambda_function_name" {
  description = "Custom Authorizer Lambda function name"
  default     = "CustomAuthorizer"
}

variable "account_id" {
  description = "AWS account ID"
  default     = "339713138979"
}
