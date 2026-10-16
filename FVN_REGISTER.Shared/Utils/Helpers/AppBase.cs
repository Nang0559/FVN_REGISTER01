using FVN_REGISTER.Shared.Services.Language;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Shared.Utils.Helpers
{
    /// <summary>
    /// Common base for application pages/components.
    /// Language changes are handled here so every AppComponentBase consumer re-renders immediately.
    /// </summary>
    public abstract class AppBase : ComponentBase, IDisposable
    {
        [Inject] protected ILoggerFactory LoggerFactory { get; set; } = default!;
        [Inject] protected IConfiguration Configuration { get; set; } = default!;
        [Inject] protected ILanguageService LanguageService { get; set; } = default!;

        protected ILogger Logger = default!;
        protected bool Debug => Configuration.GetValue<bool>("AuthDebug:Enabled");
        protected string ComponentName => GetType().Name;

        protected override void OnInitialized()
        {
            Logger = LoggerFactory.CreateLogger(GetType());
            LanguageService.LanguageChanged += OnLanguageChanged;
        }

        protected virtual void OnLanguageChanged(object? sender, EventArgs e) =>
            _ = InvokeAsync(StateHasChanged);

        public virtual void Dispose() => LanguageService.LanguageChanged -= OnLanguageChanged;
    }
}
