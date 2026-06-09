using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CharM.Web.Rendering;

public static class InteractiveRenderSettings
{
    public static IComponentRenderMode? AppInteractiveServer { get; private set; } =
        RenderMode.InteractiveServer;

    public static void ConfigureBlazorHybrid()
        => AppInteractiveServer = null;
}
