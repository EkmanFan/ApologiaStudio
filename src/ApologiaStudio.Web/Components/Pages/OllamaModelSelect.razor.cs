using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using static Microsoft.AspNetCore.Components.Web.RenderMode;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using ApologiaStudio.Web;
using ApologiaStudio.Web.Components;
using ApologiaStudio.Web.Components.Layout;
using ApologiaStudio.AgentRuntime.Routing.Semantic;

namespace ApologiaStudio.Web.Components.Pages;

public partial class OllamaModelSelect
{
    [Parameter, EditorRequired]
    public string Id { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public IReadOnlyList<OllamaLocalModel> Models { get; set; } =
        Array.Empty<OllamaLocalModel>();

    [Parameter, EditorRequired]
    public string Value { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public bool IsDisabled { get; set; }

    private bool HasUnavailableValue =>
        !string.IsNullOrWhiteSpace(Value) &&
        !Models.Any(
            model =>
                string.Equals(
                    model.Name,
                    Value,
                    StringComparison.OrdinalIgnoreCase));

    private Task HandleChangedAsync(ChangeEventArgs eventArgs)
    {
        return ValueChanged.InvokeAsync(
            eventArgs.Value?.ToString() ?? string.Empty);
    }
}
