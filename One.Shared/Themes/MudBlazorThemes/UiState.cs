namespace One.Shared.Themes.MudBlazorThemes
{
    public class UiState
    {
        public bool IsDark { get; set; } = false;
        public bool IsRtl { get; set; } = true;

        public event Action? OnChange;

        private void Notify() => OnChange?.Invoke();

        public void ToggleDark()
        {
            IsDark = !IsDark; Notify();
        }

        public void ToggleRtl()
        {
            IsRtl = !IsRtl; Notify();
        }

        // اختيارى: setters مفيدة لو هتحفظ/تسترجع من تخزين محلى
        public void SetDark(bool value) { IsDark = value; Notify(); }

        public void SetRtl(bool value)
        {
            IsRtl = value; Notify();
        }
    }
}