using Microsoft.AspNetCore.Components;
using One.Domain.Enums;

namespace One.Shared.Models;

public sealed class ChildData
{
    public EventCallback<ChildRequestData> CallPapaEvent { get; set; }

    public Task CallPapa(ChildRequestData request) =>
        CallPapaEvent.InvokeAsync(new ChildRequestData
        {
            RequestType = request.RequestType,
            Param1 = request.Param1,
            Param2 = request.Param2
        });
}

public sealed class ChildRequestData
{
    public ChildRequest RequestType { get; set; }
    public object? Param1 { get; set; }
    public object? Param2 { get; set; }
}
