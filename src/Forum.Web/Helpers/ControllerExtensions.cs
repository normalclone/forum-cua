using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Forum.Web.Helpers;

public static class ControllerExtensions
{
    /// <summary>Render một partial view ra chuỗi HTML (để trả về qua AJAX).</summary>
    public static async Task<string> RenderViewToStringAsync(this Controller controller, string viewName, object model)
    {
        controller.ViewData.Model = model;
        var engine = controller.HttpContext.RequestServices.GetRequiredService<ICompositeViewEngine>();
        var result = engine.FindView(controller.ControllerContext, viewName, isMainPage: false);
        if (!result.Success)
            throw new InvalidOperationException($"Không tìm thấy view '{viewName}'.");

        await using var sw = new StringWriter();
        var viewContext = new ViewContext(controller.ControllerContext, result.View, controller.ViewData,
            controller.TempData, sw, new HtmlHelperOptions());
        await result.View.RenderAsync(viewContext);
        return sw.ToString();
    }
}
