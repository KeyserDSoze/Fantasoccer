namespace Fantasoccer.Application.Shared
{
    public sealed class LoadingIndicator
    {
        public Action? ShowOrHide { get; set; }
        public bool IsVisible { get; set; }
        public void Show()
        {
            IsVisible = true;
            ShowOrHide?.Invoke();
        }
        public void Hide()
        {
            IsVisible = false;
            ShowOrHide?.Invoke();
        }
    }
}
