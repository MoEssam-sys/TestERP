using Microsoft.AspNetCore.Components;

namespace One.Domain.Base
{
    public class TabView
    {
        public bool IsTouched { get; set; } = false;
        public Guid TabId { get; set; }
        public string Name { get; set; } = "";
        public short MenuId { get; set; }
        public RenderFragment Content { get; set; } = null!;
    }
}