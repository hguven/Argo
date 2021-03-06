﻿using Newtonsoft.Json;
using RedArrow.Argo.Client.Flurl.Shared;
using RedArrow.Argo.Client.Http.Handlers.Request;
using RedArrow.Argo.Client.Model;
using RedArrow.Argo.Client.Query;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace RedArrow.Argo.Client.Http
{
    internal class HttpRequestBuilder : IHttpRequestBuilder
    {
        private const string JsonApiHeader = "application/vnd.api+json";

        private HttpRequestModifier HttpRequestModifier { get; }

        public HttpRequestBuilder(HttpRequestModifier httpRequestModifier)
        {
            HttpRequestModifier = httpRequestModifier;
        }

        public HttpRequestMessage GetResource(Guid id, string resourceType, string include)
        {
            var path = $"{resourceType}/{id}";
            if (!string.IsNullOrEmpty(include))
            {
                path = path.SetQueryParam("include", include);
            }
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            HttpRequestModifier?.GetResource(request, id, resourceType);
            return request;
        }

        public HttpRequestMessage GetRelated(Guid id, string resourceType, string rltnName)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{resourceType}/{id}/{rltnName}");
            HttpRequestModifier?.GetRelated(request, id, resourceType, rltnName);
            return request;
        }

        public async Task<HttpRequestMessage> CreateResource(ResourceRootSingle root)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, root.Data.Type);
            HttpRequestModifier?.CreateResource(request, root);
            // Allowing the RequestModifier to set custom content, if they really wanted to
            if (request.Content == null)
            {
                request.Content = await BuildHttpContent(root);
            }
            return request;
        }

        public async Task<HttpRequestMessage> UpdateResource(Resource resource, ResourceRootSingle root)
        {
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{root.Data.Type}/{root.Data.Id}");
            HttpRequestModifier?.UpdateResource(request, resource, root);
            // Allowing the RequestModifier to set custom content, if they really wanted to
            if (request.Content == null)
            {
                request.Content = await BuildHttpContent(root);
            }
            return request;
        }

        public HttpRequestMessage DeleteResource(string resourceType, Guid id)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{resourceType}/{id}");
            HttpRequestModifier?.DeleteResource(request, id, resourceType);
            return request;
        }

        public HttpRequestMessage QueryResources(IQueryContext query, string include)
        {
            var path = query.BuildPath();
            if (!string.IsNullOrEmpty(include))
            {
                path = path.SetQueryParam("include", include);
            }

            var request = new HttpRequestMessage(HttpMethod.Get, path);
            HttpRequestModifier?.QueryResources(request, query);
            return request;
        }

        private async Task<HttpContent> BuildHttpContent(ResourceRootSingle root)
        {
            /* The streaming approach appears to have reduced the memory footprint by a decent
             * margin (maybe a third). I suspect that StringContent just copies the string to a
             * MemoryStream anyway so we're only reducing that extra step of string to stream. */
            //return new StringContent(root.ToJson(JsonSettings), Encoding.UTF8, JsonApiHeader);

            var serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            });

            var stream = new MemoryStream();
            var sr = new StreamWriter(stream);
            var writer = new JsonTextWriter(sr);
            serializer.Serialize(writer, root);
            await writer.FlushAsync();
            stream.Seek(0, SeekOrigin.Begin);
            var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue(JsonApiHeader)
            {
                CharSet = Encoding.UTF8.WebName
            };
            return content;
        }
    }
}
