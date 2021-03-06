﻿using System;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Sinoiov.OpenApi.ConfigurationSection;
using Sinoiov.OpenApi.Interfaces;
using Sinoiov.OpenApi.Options;

namespace Sinoiov.OpenApi.Implements
{
    internal class SinoiovHttpClientFactory : IHttpClientFactory
    {
        static Lazy<HttpMessageHandler> HttpMessageHandler = new Lazy<HttpMessageHandler>(() =>
        {
#if NET5_0
            if (SocketsHttpHandler.IsSupported)
            {
                return new SocketsHttpHandler();
            }
            else
            {
                return new HttpClientHandler();
            }
#else
            return new HttpClientHandler();
#endif
        });
        public HttpClient CreateClient(string name) => new HttpClient(HttpMessageHandler.Value, false);
    }

    /// <summary>
    /// 中交兴路服务工厂
    /// </summary>
    public static class SinoiovServiceFactory
    {
        /// <summary>
        /// 创建中交兴路服务
        /// </summary>
        /// <param name="sectionName">配置节名称</param>
        /// <param name="sinoiovTokenStorageService">自定义Token存取服务</param>
        /// <returns></returns>
        public static ISinoiovService CreateSinoiovService(string sectionName = null, ISinoiovTokenStorageService sinoiovTokenStorageService = null)
        {
            sectionName ??= SinoiovConfigurationSection.DefaultConfigurationSectionName;
            var configSection = System.Configuration.ConfigurationManager.GetSection(sectionName);
            var sinoiovConfigurationSection = configSection as SinoiovConfigurationSection;
            IOptions<SinoiovOptions> sinoiovOptions = sinoiovConfigurationSection.ToOptionsWrapper();
            return CreateSinoiovService(sinoiovOptions, sinoiovTokenStorageService);
        }

        /// <summary>
        /// 创建中交兴路服务
        /// </summary>
        /// <param name="sinoiovOptions"></param>
        /// <param name="sinoiovTokenStorageService">自定义Token存取服务</param>
        /// <returns></returns>
        public static ISinoiovService CreateSinoiovService(IOptions<SinoiovOptions> sinoiovOptions, ISinoiovTokenStorageService sinoiovTokenStorageService = null)
        {
            return new SinoiovService(sinoiovOptions, sinoiovTokenStorageService);
        }
    }

    /// <summary>
    /// 中交兴路服务接口
    /// </summary>
    public interface ISinoiovService : IDisposable
    {
        /// <summary>
        /// 位置信息类接口
        /// </summary>
        ISinoiovLocationService SinoiovLocationService { get; }
        /// <summary>
        /// Token服务
        /// </summary>
        ISinoiovTokenService SinoiovTokenService { get; }
    }

    internal class SinoiovService : ISinoiovService
    {
        private readonly IOptions<SinoiovOptions> sinoiovOptions;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ISinoiovHttpClient sinoiovHttpClient;
        private readonly ISinoiovSignService sinoiovSignService;
        private readonly ISinoiovCacheProviderFactory sinoiovCacheProviderFactory;
        private readonly ISinoiovTokenStorageService sinoiovTokenStorageService;
        private readonly ISinoiovOutRequestService sinoiovOutRequestService;

        public SinoiovService(
            IOptions<SinoiovOptions> sinoiovOptions,
            ISinoiovTokenStorageService customsSinoiovTokenStorageService = null
            )
        {
            this.sinoiovOptions = sinoiovOptions;
            this.httpClientFactory = new SinoiovHttpClientFactory();
            this.sinoiovHttpClient = new SinoiovHttpClient(httpClientFactory, sinoiovOptions);
            this.sinoiovSignService = new SinoiovSignService(sinoiovOptions);
            if (sinoiovOptions.Value.TokenStorageIn == Enums.SinoiovTokenStorageType.Custom)
            {
                if (customsSinoiovTokenStorageService is null)
                {
                    throw new ArgumentException("未指定自定义Token存储服务 ISinoiovTokenStorageService");
                }
                else
                {
                    this.sinoiovTokenStorageService = customsSinoiovTokenStorageService;
                }
            }
            else
            {
                this.sinoiovCacheProviderFactory = new SinoiovCacheProviderFactory(sinoiovOptions);
                this.sinoiovTokenStorageService = new SinoiovTokenStorageService(sinoiovCacheProviderFactory, sinoiovOptions);
            }


            this.sinoiovOutRequestService = new SinoiovOutRequestService(sinoiovOptions, sinoiovHttpClient, sinoiovSignService, sinoiovTokenStorageService);
        }

        public void Dispose()
        {
            sinoiovHttpClient?.Dispose();
            sinoiovTokenStorageService?.Dispose();
        }

        private ISinoiovLocationService sinoiovLocationService;

        public ISinoiovLocationService SinoiovLocationService => sinoiovLocationService ??= new SinoiovLocationService(sinoiovOutRequestService);

        public ISinoiovTokenService SinoiovTokenService => sinoiovOutRequestService;
    }
}
