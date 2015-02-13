﻿using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public static class ServiceHelpers
    {
        static string graphResourceId = ConfigurationManager.AppSettings["ida:GraphResourceId"];
        static string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        static string appKey = ConfigurationManager.AppSettings["ida:AppKey"];
        static string tenant = ConfigurationManager.AppSettings["ida:Tenant"];

        public static async Task Test(IOwinContext context)
        {
            //
            // The Scope claim tells you what permissions the client application has in the service.
            // In this case we look for a scope value of user_impersonation, or full access to the service as the user.
            //
            Claim scopeClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/scope");

            if (scopeClaim != null && scopeClaim.Value == "user_impersonation")
            {
                Claim claim = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier);
                string msg = string.Format("OK claim.Subject.Name = {0} Value = {1}", claim.Subject.Name, claim.Value);
                await context.Response.WriteAsync(msg);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
            }
            else
            {
                await context.Response.WriteAsync("The Scope claim does not contain 'user_impersonation' or scope claim not found");
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            }
        }

        public static async Task WriteErrorResponse(IOwinContext context, string error, HttpStatusCode statusCode)
        {
            JToken content = new JObject { { "error", error } };
            await WriteResponse(context, content, statusCode);
        }

        public static async Task WriteResponse(IOwinContext context, JToken content, HttpStatusCode statusCode)
        {
            context.Response.StatusCode = (int)statusCode;

            if (content != null)
            {
                string callback = context.Request.Query["callback"];

                string contentType;
                string responseString;
                if (string.IsNullOrEmpty(callback))
                {
                    responseString = content.ToString();
                    contentType = "application/json";
                }
                else
                {
                    responseString = string.Format("{0}({1})", callback, content);
                    contentType = "application/javascript";
                }

                context.Response.Headers.Add("Pragma", new string[] { "no-cache" });
                context.Response.Headers.Add("Cache-Control", new string[] { "no-cache" });
                context.Response.Headers.Add("Expires", new string[] { "0" });
                context.Response.ContentType = contentType;

                await context.Response.WriteAsync(responseString);
            }
        }

        public static async Task<ActiveDirectoryClient> GetActiveDirectoryClient()
        {
            string authority = string.Format(aadInstance, tenant);

            AuthenticationContext authContext = new AuthenticationContext(authority);

            ClientCredential clientCredential = new ClientCredential(clientId, appKey);
            AuthenticationResult result = await authContext.AcquireTokenAsync(graphResourceId, clientCredential);

            string accessToken = result.AccessToken;

            Uri serviceRoot = new Uri(new Uri(graphResourceId), tenant);

            ActiveDirectoryClient activeDirectoryClient = new ActiveDirectoryClient(serviceRoot, () => { return Task.FromResult(accessToken); });

            return activeDirectoryClient;
        }
    }
}