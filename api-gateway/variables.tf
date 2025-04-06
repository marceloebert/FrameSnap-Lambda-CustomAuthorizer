variable "region" {
  description = "AWS region"
  default     = "us-east-1"
}

variable "eks_service_endpoint" {
  description = "EKS public endpoint (sem /videos no final)"
  default     = "http://a46278021035f45099372d1901e13158-107424252.us-east-1.elb.amazonaws.com/videos"
}

variable "login_service_endpoint" {
  description = "EKS login service endpoint (rota /login)"
  default     = "http://aaac2fcb5f97e483786be9c190d47c41-1267190571.us-east-1.elb.amazonaws.com/login"
}

variable "lambda_function_name" {
  description = "Custom Authorizer Lambda function name"
  default     = "CustomAuthorizer"
}

variable "account_id" {
  description = "AWS account ID"
  default     = "114692541707"
}
