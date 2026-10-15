using Microsoft.AspNetCore.Components;
using One.Domain.Enums;

namespace One.Shared.OneComponents
{
    public partial class OneButtonsBar
    {
        [Parameter] public bool UpdateState { get; set; }
        [Parameter] public string SaveBtnTxt { get; set; } = "";

        [Parameter] public EventCallback<OneBtnClick> OnBtnClick { get; set; }

        //_@_@

        [Parameter] public bool AllBtnsDisabled { get; set; }

        [Parameter] public bool IsSaveBtnDisabled { get; set; }
        [Parameter] public bool IsClearBtnDisabled { get; set; }
        [Parameter] public bool IsUpdateBtnDisabled { get; set; }
        [Parameter] public bool IsDeleteBtnDisabled { get; set; }
        [Parameter] public bool ShowUpdateBtn { get; set; } = false;
        [Parameter] public bool ShowDeleteBtn { get; set; } = false;
        [Parameter] public bool ShowClearBtn { get; set; } = false;

        //string Tr(string value) => _localizer.Keys[value];

        //~~~

        protected override async Task OnInitializedAsync()
        {
            if (string.IsNullOrWhiteSpace(SaveBtnTxt))
                SaveBtnTxt = loc["Save"];
            await Task.CompletedTask;
        }
    }
}