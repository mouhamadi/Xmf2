﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp.Portable;
using RestSharp.Portable.Authenticators;
using RestSharp.Portable.Authenticators.OAuth2;
using RestSharp.Portable.Authenticators.OAuth2.Configuration;
using RestSharp.Portable.Authenticators.OAuth2.Infrastructure;
using RestSharp.Portable.Authenticators.OAuth2.Models;
using RestSharp.Portable.Authenticators.OAuth2Password.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RestSharp.Portable.Authenticators.OAuth2Password
{
    public abstract class OAuth2PasswordClient : IPasswordClient
    {
        private const string AccessTokenKey = "access_token";
        private const string RefreshTokenKey = "refresh_token";
        private const string ExpiresKey = "expires_in";
        private const string TokenTypeKey = "token_type";
        private const string UsernameKey = "username";
        private const string PasswordKey = "password";

        private const string GrantTypeAuthorizationKey = "password";
        private const string GrantTypeRefreshTokenKey = "refresh_token";

        private readonly IRequestFactory _factory;

        /// <summary>
        /// Client configuration object.
        /// </summary>
        public IPasswordClientConfiguration Configuration { get; private set; }

        /// <summary>
        /// Friendly name of provider (OAuth2 service).
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Access token returned by provider. Can be used for further calls of provider API.
        /// </summary>
        public string AccessToken { get; protected set; }

        /// <summary>
        /// Refresh token returned by provider. Can be used for further calls of provider API.
        /// </summary>
        public string RefreshToken { get; protected set; }

        /// <summary>
        /// Token type returned by provider. Can be used for further calls of provider API.
        /// </summary>
        public string TokenType { get; private set; }

        /// <summary>
        /// The time when the access token expires
        /// </summary>
        public DateTime? ExpiresAt { get; protected set; }

        /// <summary>
        /// A safety margin that's used to see if an access token is expired
        /// </summary>
        public TimeSpan ExpirationSafetyMargin { get; set; }

        /// <summary>
        /// Gets the instance of the request factory.
        /// </summary>
        protected IRequestFactory Factory
        {
            get { return _factory; }
        }

        private string GrantType { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Client"/> class.
        /// </summary>
        /// <param name="factory">The factory.</param>
        /// <param name="configuration">The configuration.</param>
        protected OAuth2PasswordClient(IRequestFactory factory, IPasswordClientConfiguration configuration)
        {
            ExpirationSafetyMargin = TimeSpan.FromSeconds(5);
            _factory = factory;
            Configuration = configuration;
        }

        public async Task<UserInfo> GetUserInfo(string userName, string password)
        {
            GrantType = GrantTypeAuthorizationKey;
            Dictionary<string, string> dicoParameter = new Dictionary<string, string>();
            dicoParameter.Add(UsernameKey, userName);
            dicoParameter.Add(PasswordKey, password);
            return await this.GetUserInfo(dicoParameter.ToLookup(y => y.Key, y => y.Value));
        }

        public async Task<UserInfo> GetUserInfo(string refreshToken)
        {
            this.RefreshToken = refreshToken;
            GrantType = GrantTypeRefreshTokenKey;
            Dictionary<string, string> dicoParameter = new Dictionary<string, string>();
            dicoParameter.Add(RefreshTokenKey, refreshToken);
            return await this.GetUserInfo(dicoParameter.ToLookup(y => y.Key, y => y.Value));
        }

        /// <summary>
        /// Obtains user information using RestSharp.Portable.Authenticators.OAuth2 service and data provided via callback request.
        /// </summary>
        /// <param name="parameters">Callback request payload (parameters).</param>
        public async Task<UserInfo> GetUserInfo(ILookup<string, string> parameters = null)
        {
            if (parameters == null)
                parameters = new Dictionary<string, string>().ToLookup(y => y.Key, y => y.Value);

            if (this.GrantType.IsEmpty())
                GrantType = GrantTypeAuthorizationKey;
            CheckError(parameters);
            await QueryAccessToken(parameters);
            return await GetUserInfo();
        }

        public async Task<string> GetToken(string userName, string password)
        {
            GrantType = GrantTypeAuthorizationKey;
            Dictionary<string, string> dicoParameter = new Dictionary<string, string>();
            dicoParameter.Add(UsernameKey, userName);
            dicoParameter.Add(PasswordKey, password);
            return await this.GetToken(dicoParameter.ToLookup(y => y.Key, y => y.Value));
        }

        public async Task<string> GetToken(string refreshToken)
        {
            this.RefreshToken = refreshToken;
            GrantType = GrantTypeRefreshTokenKey;
            Dictionary<string, string> dicoParameter = new Dictionary<string, string>();
            dicoParameter.Add(RefreshTokenKey, refreshToken);
            return await this.GetToken(dicoParameter.ToLookup(y => y.Key, y => y.Value));
        }

        /// <summary>
        /// Issues query for access token and returns access token.
        /// </summary>
        /// <param name="parameters">Callback request payload (parameters).</param>
        public async Task<string> GetToken(ILookup<string, string> parameters)
        {
            if (parameters == null)
                parameters = new Dictionary<string, string>().ToLookup(y => y.Key, y => y.Value);

            if (this.GrantType.IsEmpty())
                GrantType = GrantTypeAuthorizationKey;
            CheckError(parameters);
            await QueryAccessToken(parameters);
            return AccessToken;
        }

        /// <summary>
        /// Get the current access token - and optinally refreshes it if it is expired
        /// </summary>
        /// <param name="refreshToken">The refresh token to use (null == default)</param>
        /// <param name="forceUpdate">Enfore an update of the access token?</param>
        /// <param name="safetyMargin">A custom safety margin to check if the access token is expired</param>
        /// <returns></returns>
        public async Task<string> GetCurrentToken(string refreshToken = null, bool forceUpdate = false, TimeSpan? safetyMargin = null)
        {
            bool refreshRequired =
                forceUpdate
                || (ExpiresAt != null && DateTime.Now >= (ExpiresAt - (safetyMargin ?? ExpirationSafetyMargin)))
                || String.IsNullOrEmpty(AccessToken);

            if (refreshRequired)
            {
                string refreshTokenValue;
                if (!string.IsNullOrEmpty(refreshToken))
                    refreshTokenValue = refreshToken;
                else if (!string.IsNullOrEmpty(RefreshToken))
                    refreshTokenValue = RefreshToken;
                else
                    throw new Exception("Token never fetched and refresh token not provided.");

                var parameters = new Dictionary<string, string>() {
                    { RefreshTokenKey, refreshTokenValue },
                };

                GrantType = GrantTypeRefreshTokenKey;
                await QueryAccessToken(parameters.ToLookup(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
            }

            return AccessToken;
        }

        /// <summary>
        /// Defines URI of service which issues access token.
        /// </summary>
        protected abstract Endpoint AccessTokenServiceEndpoint { get; }

        /// <summary>
        /// Defines URI of service which allows to obtain information about user 
        /// who is currently logged in.
        /// </summary>
        protected abstract Endpoint UserInfoServiceEndpoint { get; }

        private void CheckError(ILookup<string, string> parameters)
        {
            if (parameters == null)
                return;

            const string errorFieldName = "error";

            var error = parameters[errorFieldName].ToList();
            if (error.Any(x => !string.IsNullOrEmpty(x)))
                throw new UnexpectedResponseException(errorFieldName, string.Join("\n", error));
        }

        /// <summary>
        /// Issues query for access token and parses response.
        /// </summary>
        /// <param name="parameters">Callback request payload (parameters).</param>
        private async Task QueryAccessToken(ILookup<string, string> parameters)
        {
            var client = _factory.CreateClient(AccessTokenServiceEndpoint);
            var request = _factory.CreateRequest(AccessTokenServiceEndpoint, HttpMethod.Post);

            BeforeGetAccessToken(new PasswordBeforeAfterRequestArgs
            {
                Client = client,
                Request = request,
                Parameters = parameters,
                Configuration = Configuration
            });

            var response = await client.ExecuteAndVerify(request);

            var content = response.GetContent();
            AccessToken = ParseAccessTokenResponse(content);

            RefreshToken = ParseStringResponse(content, new[] { RefreshTokenKey })[RefreshTokenKey].FirstOrDefault();
            TokenType = ParseStringResponse(content, new[] { TokenTypeKey })[TokenTypeKey].FirstOrDefault();

            var expiresIn = ParseStringResponse(content, new[] { ExpiresKey })[ExpiresKey].Select(x => Convert.ToInt32(x, 10)).FirstOrDefault();
            ExpiresAt = (expiresIn != 0 ? (DateTime?)DateTime.Now.AddSeconds(expiresIn) : null);

            AfterGetAccessToken(new PasswordBeforeAfterRequestArgs
            {
                Response = response,
                Parameters = parameters
            });
        }

        /// <summary>
        /// Parse the access token response using either JSON or form url encoded parameters
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        protected virtual string ParseAccessTokenResponse(string content)
        {
            return ParseStringResponse(content, AccessTokenKey);
        }

        /// <summary>
        /// Parse the response, search for a key and return its value.
        /// </summary>
        /// <param name="content">The content to parse</param>
        /// <param name="key">The key to query</param>
        /// <returns></returns>
        /// <exception cref="UnexpectedResponseException">Thrown when the key wasn't found</exception>
        protected static string ParseStringResponse(string content, string key)
        {
            var values = ParseStringResponse(content, new[] { key })[key].ToList();
            if (values.Count == 0)
                throw new UnexpectedResponseException(key);
            return values.First();
        }

        /// <summary>
        /// Parse the response for a given key/value using either JSON or form url encoded parameters
        /// </summary>
        /// <param name="content"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        protected static ILookup<string, string> ParseStringResponse(string content, params string[] keys)
        {
            var result = new List<KeyValuePair<string, string>>();
            try
            {
                // response can be sent in JSON format
                var jobj = JObject.Parse(content);
                foreach (var key in keys)
                {
                    foreach (var token in jobj.SelectTokens(key))
                        if (token.HasValues)
                        {
                            foreach (var value in token.Values())
                                result.Add(new KeyValuePair<string, string>(key, (string)value));
                        }
                        else
                            result.Add(new KeyValuePair<string, string>(key, (string)token));
                }
            }
            catch (JsonReaderException)
            {
                // or it can be in "query string" format (param1=val1&param2=val2)
                var collection = content.ParseQueryString();
                foreach (var key in keys)
                {
                    foreach (var item in collection[key])
                        result.Add(new KeyValuePair<string, string>(key, item));
                }
            }
            return result.ToLookup(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Should return parsed <see cref="UserInfo"/> using content received from provider.
        /// </summary>
        /// <param name="content">The content which is received from provider.</param>
        protected abstract UserInfo ParseUserInfo(string content);

        /// <summary>
        /// Called just before building the request URI when everything is ready.
        /// Allows to add extra parameters to request or do any other needed preparations.
        /// </summary>
        protected virtual async Task BeforeGetLoginLinkUri(PasswordBeforeAfterRequestArgs args)
        {
            await Task.Factory.StartNew(() => { });
        }

        /// <summary>
        /// Called before the request to get the access token
        /// </summary>
        /// <param name="args"></param>
        protected virtual void BeforeGetAccessToken(PasswordBeforeAfterRequestArgs args)
        {
            args.Request.AddObject(new
            {
                grant_type = GrantType
            });

            if(!Configuration.ClientId.IsEmpty())
            { 
                args.Request.AddObject(new
                {
                    client_id = Configuration.ClientId
                });
            }

            if (!Configuration.ClientSecret.IsEmpty())
            {
                args.Request.AddObject(new
                {
                    client_secret = Configuration.ClientSecret
                });
            }

            if (GrantType == GrantTypeRefreshTokenKey)
            {
                args.Request.AddObject(new
                {
                    refresh_token = args.Parameters.GetOrThrowUnexpectedResponse(RefreshTokenKey),
                });
            }
            else
            {
                args.Request.AddObject(new
                {
                    username = args.Parameters.GetOrThrowUnexpectedResponse(UsernameKey),
                    password = args.Parameters.GetOrThrowUnexpectedResponse(PasswordKey),
                });
            }
        }

        /// <summary>
        /// Called just after obtaining response with access token from service.
        /// Allows to read extra data returned along with access token.
        /// </summary>
        protected virtual void AfterGetAccessToken(PasswordBeforeAfterRequestArgs args)
        {
        }

        /// <summary>
        /// Called just before issuing request to service when everything is ready.
        /// Allows to add extra parameters to request or do any other needed preparations.
        /// </summary>
        protected virtual void BeforeGetUserInfo(PasswordBeforeAfterRequestArgs args)
        {
        }

        /// <summary>
        /// Obtains user information using provider API.
        /// </summary>
        protected virtual async Task<UserInfo> GetUserInfo()
        {
            var client = _factory.CreateClient(UserInfoServiceEndpoint);
            client.Authenticator = new OAuth2PasswordAuthorizationRequestHeaderAuthenticator(this, "Bearer");
            var request = _factory.CreateRequest(UserInfoServiceEndpoint);

            BeforeGetUserInfo(new PasswordBeforeAfterRequestArgs
            {
                Client = client,
                Request = request,
                Configuration = Configuration
            });

            var response = await client.ExecuteAndVerify(request);

            var result = ParseUserInfo(response.GetContent());
            result.ProviderName = Name;

            return result;
        }
    }
}
