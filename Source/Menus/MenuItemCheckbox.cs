namespace RAGENativeUI.Menus
{
    using Graphics = Rage.Graphics;
    
    public class MenuItemCheckbox : MenuItem
    {
        public delegate void CheckedChangedEventHandler(MenuItem sender, bool isChecked);

        private bool isChecked;

        public event CheckedChangedEventHandler CheckedChanged;
        public event CheckedChangedEventHandler Checked;
        public event CheckedChangedEventHandler Unchecked;

        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                if (value == isChecked)
                    return;

                isChecked = value;
                OnCheckChanged(isChecked);
            }
        }

        public MenuItemCheckbox(string text) : base(text)
        {
        }

        protected internal override bool OnAccept()
        {
            IsChecked = !IsChecked;

            return base.OnAccept();
        }

        protected internal override void OnDraw(Graphics graphics, ref float x, ref float y)
        {
            if (!IsVisible)
                return;

            Parent.Style.DrawItemCheckbox(graphics, this, ref x, ref y);
        }

        protected virtual void OnCheckChanged(bool isChecked)
        {
            CheckedChanged?.Invoke(this, isChecked);
            if (isChecked)
                Checked?.Invoke(this, isChecked);
            else
                Unchecked?.Invoke(this, isChecked);
        }
    }
}

