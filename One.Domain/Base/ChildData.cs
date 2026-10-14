using Microsoft.AspNetCore.Components;
using One.Domain.Enums;

namespace One.Domain.Base
{
    public class ChildData
    {
        public EventCallback<ChildRequestData> CallPapaEvent { get; set; }

        // 🔫 ღ
        public async Task CallPapa(ChildRequestData req)
        {
            await CallPapaEvent.InvokeAsync(new ChildRequestData { RequestType = req.RequestType, Param1 = req.Param1, Param2 = req.Param2 });
        }
    }

    public class ChildRequestData
    {
        public ChildRequest RequestType { get; set; }
        public object? Param1 { get; set; } = null;
        public object? Param2 { get; set; } = null;
    }
}