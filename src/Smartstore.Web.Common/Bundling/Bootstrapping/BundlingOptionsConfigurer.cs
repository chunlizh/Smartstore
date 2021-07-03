﻿using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Autofac;
using Smartstore.Core.Theming;
using Smartstore.Engine;
using Smartstore.Web.Bundling;

namespace Smartstore.Web.Bootstrapping
{
    internal class BundlingOptionsConfigurer : Disposable, IConfigureOptions<BundlingOptions>
    {
        private readonly IApplicationContext _appContext;
        private readonly IAssetFileProvider _fileProvider;
        private readonly IOptionsMonitorCache<BundlingOptions> _optionsCache;
        private readonly Work<ThemeSettings> _themeSettings;

        private BundlingOptions _prevOptions;
        private IDisposable _callback;

        public BundlingOptionsConfigurer(
            IApplicationContext appContext,
            IAssetFileProvider fileProvider,
            IOptionsMonitorCache<BundlingOptions> optionsCache,
            Work<ThemeSettings> themeSettings)
        {
            _appContext = appContext;
            _fileProvider = fileProvider;
            _optionsCache = optionsCache;
            _themeSettings = themeSettings;
        }

        public void Configure(BundlingOptions options)
        {
            // TODO: (mh) (core) Update BundlingOptions whenever theme settings change by calling this method from controller with current options.
            // TODO: (core) Invalidate bundle cache whenever a corresponding ThemeSettings property change (in ThemeController)

            var section = _appContext.Configuration.GetSection("Bundling");
            section.Bind(options);

            var env = _appContext.HostEnvironment;
            var themeSettings = _themeSettings.Value;

            bool? bundlingEnabled = null;
            if (themeSettings.BundleOptimizationEnabled > 0)
            {
                bundlingEnabled = themeSettings.BundleOptimizationEnabled > 1;
            }

            bool? diskCachingEnabled = null;
            if (themeSettings.AssetCachingEnabled > 0)
            {
                diskCachingEnabled = themeSettings.AssetCachingEnabled > 1;
            }

            if (options.EnableAutoPrefixer == null && options.AlwaysDisableAutoPrefixerInDevMode && env.IsDevelopment())
            {
                options.EnableAutoPrefixer = false;
            }

            options.EnableBundling ??= bundlingEnabled ?? !env.IsDevelopment();
            options.EnableClientCache ??= !env.IsDevelopment();
            options.EnableDiskCache ??= diskCachingEnabled ?? !env.IsDevelopment();
            options.EnableMinification ??= bundlingEnabled ?? !env.IsDevelopment();
            options.EnableAutoPrefixer ??= bundlingEnabled ?? !env.IsDevelopment();
            options.FileProvider ??= _fileProvider;

            if (_prevOptions != null)
            {
                // It's an appsettings.json change. Check if we need to invalidate the cache.
                if (_prevOptions.EnableMinification != options.EnableMinification || _prevOptions.EnableAutoPrefixer != options.EnableAutoPrefixer)
                {
                    // Cannot pass in ctor --> circular dependency exception!
                    _appContext.Services.Resolve<IBundleCache>().ClearAsync().Await();
                }
            }

            _callback = _appContext.Configuration.GetReloadToken().RegisterChangeCallback(OnChange, options);
            _prevOptions = options;
        }

        private void OnChange(object state)
        {
            _prevOptions = (BundlingOptions)state;
            _optionsCache.TryRemove(Options.DefaultName);
        }

        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                _callback?.Dispose();
            }
        }
    }
}
