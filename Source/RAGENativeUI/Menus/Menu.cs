namespace RAGENativeUI.Menus
{
#if RPH1
    extern alias rph1;
    using GameControl = rph1::Rage.GameControl;
#else
    /** REDACTED **/
#endif

    using System;
    using System.Linq;
    using System.Reflection;
    using System.ComponentModel;
    using System.Collections.ObjectModel;
    
    using RAGENativeUI.Menus.Themes;

    /// <include file='..\Documentation\RAGENativeUI.Menus.Menu.xml' path='D/Menu/Doc/*' />
    public class Menu : IDisposable, INotifyPropertyChanged
    {
        public delegate void ForEachItemOnScreenDelegate(MenuItem item, int index);


        public static bool IsAnyMenuVisible => MenuManager.IsAnyMenuVisible;
        public static readonly ReadOnlyCollection<GameControl> DefaultAllowedControls = Array.AsReadOnly(new[]
        {
            GameControl.MoveUpDown,
            GameControl.MoveLeftRight,
            GameControl.Sprint,
            GameControl.Jump,
            GameControl.Enter,
            GameControl.VehicleExit,
            GameControl.VehicleAccelerate,
            GameControl.VehicleBrake,
            GameControl.VehicleMoveLeftRight,
            GameControl.VehicleFlyYawLeft,
            GameControl.ScriptedFlyLeftRight,
            GameControl.ScriptedFlyUpDown,
            GameControl.VehicleFlyYawRight,
            GameControl.VehicleHandbrake,
        });


        private bool isDisposed;
        private MenuTheme theme;
        private string title;
        private string subtitle;
        private MenuItemsCollection items;
        private string currentDescription;
        private string descriptionOverride;
        private int selectedIndex;
        private MenuControls controls = new MenuControls();
        private MenuSoundsSet soundsSet = new MenuSoundsSet();
        private bool disableControlsActions = true;
        private GameControl[] allowedControls = DefaultAllowedControls.ToArray();
        private int itemsOnScreenStartIndex;
        private int itemsOnScreenEndIndex;
        private int maxItemsOnScreen = 10;
        private bool isVisible;
        private bool justOpened;

        private Menu currentParent, currentChild;

        public event PropertyChangedEventHandler PropertyChanged;
        public event TypedEventHandler<Menu, SelectedItemChangedEventArgs> SelectedItemChanged;
        public event TypedEventHandler<Menu, VisibleChangedEventArgs> VisibleChanged;

        public bool IsDisposed
        {
            get => isDisposed;
            private set
            {
                if(value != isDisposed)
                {
                    isDisposed = value;
                    OnPropertyChanged(nameof(IsDisposed));
                }
            }
        }

        public MenuTheme Theme
        {
            get => theme;
            private set
            {
                Throw.IfNull(value, nameof(value));
                if (value != theme)
                {
                    theme = value;
                    OnPropertyChanged(nameof(Theme));
                }
            }
        }

        public string Title
        {
            get => title;
            set
            {
                Throw.IfNull(value, nameof(value));
                if (value != title)
                {
                    title = value;
                    OnPropertyChanged(nameof(Title));
                }
            }
        }

        public string Subtitle   // TODO: in default theme, add Counter properties
        {
            get => subtitle;
            set
            {
                Throw.IfNull(value, nameof(value));
                if (value != subtitle)
                {
                    subtitle = value;
                    OnPropertyChanged(nameof(Subtitle));
                }
            }
        }

        public MenuItemsCollection Items
        {
            get
            {
                if(items == null)
                {
                    items = CreateItemsInstance();
                }
                return items;
            }
        }

        public string CurrentDescription
        {
            get => currentDescription;
            private set
            {
                if (value != currentDescription)
                {
                    currentDescription = value;
                    OnPropertyChanged(nameof(CurrentDescription));
                }
            }
        }

        public string DescriptionOverride
        {
            get => descriptionOverride;
            set
            {
                if (value != descriptionOverride)
                {
                    descriptionOverride = value;
                    UpdateCurrentDescription();
                    OnPropertyChanged(nameof(DescriptionOverride));
                }
            }
        }

        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                int newIndex = RPH.MathHelper.Clamp(value, 0, Items.Count);

                if (newIndex != selectedIndex)
                {
                    int oldIndex = selectedIndex;
                    selectedIndex = newIndex;
                    UpdateVisibleItemsIndices();
                    UpdateCurrentDescription();
                    OnPropertyChanged(nameof(SelectedIndex));
                    OnPropertyChanged(nameof(SelectedItem));
                    OnSelectedItemChanged(new SelectedItemChangedEventArgs(oldIndex, newIndex, Items[oldIndex], Items[newIndex]));
                }
            }
        }

        public MenuItem SelectedItem
        {
            get => (selectedIndex >= 0 && selectedIndex < Items.Count) ? Items[selectedIndex] : null;
            set => SelectedIndex = Items.IndexOf(value);
        }

        public MenuControls Controls
        {
            get => controls;
            set
            {
                if (value != controls)
                {
                    controls = value;
                    OnPropertyChanged(nameof(Controls));
                }
            }
        }

        public MenuSoundsSet SoundsSet
        {
            get => soundsSet;
            set
            {
                if (value != soundsSet)
                {
                    soundsSet = value;
                    OnPropertyChanged(nameof(SoundsSet));
                }
            }
        }

        public bool DisableControlsActions
        {
            get => disableControlsActions;
            set
            {
                if(value != disableControlsActions)
                {
                    disableControlsActions = value;
                    OnPropertyChanged(nameof(DisableControlsActions));
                }
            }
        }

        /// <summary>
        /// Gets or sets the controls that aren't disabled when <see cref="DisableControlsActions"/> is <c>true</c>.
        /// </summary>
        /// <value>
        /// A <see cref="GameControl"/>s array.
        /// </value>
        public GameControl[] AllowedControls
        {
            get => allowedControls;
            set
            {
                if(value != allowedControls)
                {
                    allowedControls = value;
                    OnPropertyChanged(nameof(AllowedControls));
                }
            }
        }

        /// <summary>
        /// Gets the index of the first item on-screen.
        /// </summary>
        public int ItemsOnScreenStartIndex // TODO: move ItemsOnScreenStartIndex to MenuTheme
        {
            get => itemsOnScreenStartIndex;
            private set
            {
                if (value != itemsOnScreenStartIndex)
                {
                    itemsOnScreenStartIndex = value;
                    OnPropertyChanged(nameof(ItemsOnScreenStartIndex));
                }
            }
        }

        /// <summary>
        /// Gets the index of the last item on-screen.
        /// </summary>
        public int ItemsOnScreenEndIndex // TODO: move ItemsOnScreenEndIndex to MenuTheme
        {
            get => itemsOnScreenEndIndex;
            private set
            {
                if (value != itemsOnScreenEndIndex)
                {
                    itemsOnScreenEndIndex = value;
                    OnPropertyChanged(nameof(ItemsOnScreenEndIndex));
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of items on-screen.
        /// </summary>
        public int MaxItemsOnScreen // TODO: move MaxItemsOnScreen to MenuTheme
        {
            get => maxItemsOnScreen;
            set
            {
                Throw.IfNegative(value, nameof(value));

                if (value != maxItemsOnScreen)
                {
                    maxItemsOnScreen = value;
                    UpdateVisibleItemsIndices();
                    OnPropertyChanged(nameof(MaxItemsOnScreen));
                }
            }
        }

        public int NumberOfItemsOnScreen // TODO: move NumberOfItemsOnScreen to MenuTheme
        {
            get
            {
                int count = 0;
                ForEachItemOnScreen((item, index) => count++);
                return count;
            }
        }

        public bool IsAnyItemOnScreen => IsVisible && Items.Count > 0 && MaxItemsOnScreen != 0 && Items.Any(i => i.IsVisible); // TODO: move IsAnyItemOnScreen to MenuTheme

        public bool IsVisible
        {
            get => isVisible;
            private set
            {
                if (value != isVisible)
                {
                    isVisible = value;
                    OnPropertyChanged(nameof(IsVisible));
                    OnVisibleChanged(new VisibleChangedEventArgs(isVisible));
                }
            }
        }

        /// <summary>
        /// Gets whether this <see cref="Menu"/> is visible or any child <see cref="Menu"/> in the hierarchy is visible.
        /// </summary>
        public bool IsAnyChildMenuVisible // TODO: invoke PropertyChanged for IsAnyChildMenuVisible
        {
            get
            {
                return IsVisible || (currentChild != null && currentChild.IsAnyChildMenuVisible);
            }
        }

        public bool JustOpened
        {
            get => justOpened;
            private set
            {
                if (value != justOpened)
                {
                    justOpened = value;
                    OnPropertyChanged(nameof(JustOpened));
                }
            }
        }

        public dynamic Metadata { get; } = new Metadata();

        public Menu(string title, string subtitle)
        {
            Throw.IfNull(title, nameof(title));
            Throw.IfNull(subtitle, nameof(subtitle));

            Title = title;
            Subtitle = subtitle;
            SetTheme<MenuDefaultTheme>();

            MenuManager.AddMenu(this);
        }

        ~Menu()
        {
            Dispose(false);
        }

        public void Show() => Show(null);
        public void Show(Menu parent)
        {
            currentParent = parent;

            if (parent != null)
            {
                parent.currentChild = this;
                parent.IsVisible = false;
            }

            IsVisible = true;
            JustOpened = true;
        }

        // hides child menus too
        public void Hide() => Hide(false);
        public void Hide(bool showParent)
        {
            if (currentChild != null)
                currentChild.Hide(false);

            if (showParent && currentParent != null)
            {
                currentParent.Show(currentParent.currentParent);
            }

            currentParent = null;
            currentChild = null;
            IsVisible = false;
        }
        
        // only called if the Menu is visible
        protected internal virtual void OnProcess()
        {
            // don't process in the tick the menu was opened
            if (JustOpened)
            {
                JustOpened = false;
                return;
            }

            if (DisableControlsActions)
            {
                DisableControls();
            }

            ProcessInput();
            
            for (int i = 0; i < Items.Count; i++)
            {
                MenuItem item = Items[i];
                if (item != null)
                {
                    item.IsSelected = i == SelectedIndex;
                    item.OnProcess();
                }
            }
        }

        protected virtual void DisableControls()
        {
            N.DisableAllControlActions(0);

            for (int i = 0; i < AllowedControls.Length; i++)
            {
                N.EnableControlAction(0, (int)AllowedControls[i], true);
            }
        }

        protected virtual void ProcessInput()
        {
            if (Controls != null && IsVisible)
            {
                if (IsAnyItemOnScreen)
                {
                    if (Controls.PreviousItem?.WasJustPressed(true) ?? false)
                    {
                        MoveToPreviousItem();
                    }

                    if (Controls.NextItem?.WasJustPressed(true) ?? false)
                    {
                        MoveToNextItem();
                    }

                    if (Controls.ScrollerNextValue?.WasJustPressed(true) ?? false)
                    {
                        ScrollToNextValue();
                    }

                    if (Controls.ScrollerPreviousValue?.WasJustPressed(true) ?? false)
                    {
                        ScrollToPreviousValue();
                    }
                }

                if (Controls.Accept?.WasJustPressed() ?? false)
                {
                    Accept();
                }

                if (Controls.Back?.WasJustPressed() ?? false)
                {
                    Back();
                }
            }
        }

        protected virtual void MoveToPreviousItem()
        {
            SelectedItem?.OnMovingToPreviousItem();

            int newIndex = SelectedIndex - 1;

            int min = GetMinItemWithInputIndex();

            // get previous if current isn't visible
            while (newIndex >= min && (!Items[newIndex].IsVisible || (Items[newIndex].IsDisabled && Items[newIndex].IsSkippedIfDisabled)))
                newIndex--;

            if (newIndex < min)
                newIndex = GetMaxItemWithInputIndex();

            SelectedIndex = newIndex;

            SoundsSet?.Up?.Play();
        }

        protected virtual void MoveToNextItem()
        {
            SelectedItem?.OnMovingToNextItem();

            int newIndex = SelectedIndex + 1;

            int max = GetMaxItemWithInputIndex();

            // get next if current isn't visible
            while (newIndex <= max && (!Items[newIndex].IsVisible || (Items[newIndex].IsDisabled && Items[newIndex].IsSkippedIfDisabled)))
                newIndex++;

            if (newIndex > max)
                newIndex = GetMinItemWithInputIndex();

            SelectedIndex = newIndex;

            SoundsSet?.Down?.Play();
        }

        protected virtual void ScrollToNextValue()
        {
            SelectedItem?.OnScrollingToNextValue();

            SoundsSet?.Right?.Play();
        }

        protected virtual void ScrollToPreviousValue()
        {
            SelectedItem?.OnScrollingToPreviousValue();

            SoundsSet?.Left?.Play();
        }

        protected virtual void Accept()
        {
            SelectedItem?.OnAccept();

            SoundsSet?.Accept?.Play();
        }

        protected virtual void Back()
        {
            SelectedItem?.OnBack();

            Hide(true);
            SoundsSet?.Back?.Play();
        }

        internal void UpdateVisibleItemsIndices() // TODO: move UpdateVisibleItemsIndices to MenuTheme
        {
            if (MaxItemsOnScreen == 0 || Items.All(i => !i.IsVisible))
            {
                ItemsOnScreenStartIndex = -1;
                ItemsOnScreenEndIndex = -1;
                return;
            }
            else if (MaxItemsOnScreen >= Items.Count)
            {
                ItemsOnScreenStartIndex = 0;
                ItemsOnScreenEndIndex = Items.Count - 1;
                return;
            }

            int index = SelectedIndex;

            if (index < ItemsOnScreenStartIndex)
            {
                ItemsOnScreenStartIndex = index;
                int count = 0;
                for (int i = ItemsOnScreenStartIndex; i < Items.Count; i++)
                {
                    if (Items[i].IsVisible)
                    {
                        count++;
                        if (count == MaxItemsOnScreen)
                        {
                            ItemsOnScreenEndIndex = i;
                        }
                    }
                }
            }
            else if (index > ItemsOnScreenEndIndex)
            {
                ItemsOnScreenEndIndex = index;
                int count = 0;
                for (int i = ItemsOnScreenEndIndex; i >= 0; i--)
                {
                    if (Items[i].IsVisible)
                    {
                        count++;
                        if (count == MaxItemsOnScreen)
                        {
                            ItemsOnScreenStartIndex = i;
                        }
                    }
                }
            }
            else
            {
                int count = 0;
                for (int i = ItemsOnScreenStartIndex; i < Items.Count; i++)
                {
                    if (Items[i].IsVisible)
                    {
                        count++;
                        if (count == MaxItemsOnScreen)
                        {
                            ItemsOnScreenEndIndex = i;
                        }
                    }
                }
            }

            int min = GetMinVisibleItemIndex();
            int max = GetMaxVisibleItemIndex();

            if (ItemsOnScreenStartIndex < min)
                ItemsOnScreenStartIndex = min;
            if (ItemsOnScreenEndIndex > max)
                ItemsOnScreenEndIndex = max;

            Throw.InvalidOperationIf(ItemsOnScreenEndIndex < ItemsOnScreenStartIndex, $"ItemsOnScreenEndIndex({ItemsOnScreenEndIndex}) < ItemsOnScreenStartIndex({ItemsOnScreenStartIndex}): this should never happen.");
        }

        internal void UpdateCurrentDescription()
        {
            if(DescriptionOverride == null)
            {
                MenuItem item = SelectedItem;
                if(item != null && item.IsVisible)
                {
                    CurrentDescription = item.Description;
                }
                else
                {
                    CurrentDescription = null;
                }
            }
            else
            {
                CurrentDescription = DescriptionOverride;
            }
        }

        /// <summary>
        /// Executes the specified action for each item that is currently on-screen.
        /// An item is considered on-screen if the current menu is visible, its <see cref="MenuItem.IsVisible"/> property is <c>true</c> and is currently being drawn.
        /// </summary>
        /// <param name="action">The action to execute on each item.</param>
        public void ForEachItemOnScreen(ForEachItemOnScreenDelegate action) // TODO: move ForEachItemOnScreen to MenuTheme
        {
            if (ItemsOnScreenStartIndex == -1 || ItemsOnScreenEndIndex == -1)
                return;

            if (Items.Count > 0 && IsAnyItemOnScreen)
            {
                for (int i = ItemsOnScreenStartIndex; i <= ItemsOnScreenEndIndex; i++)
                {
                    MenuItem item = Items[i];

                    if (item != null)
                    {
                        if (!item.IsVisible)
                        {
                            continue;
                        }

                        action?.Invoke(item, i);
                    }
                }
            }
        }
        
        public MenuTheme SetTheme(Type themeType)
        {
            Throw.IfNull(themeType, nameof(themeType));
            Throw.ArgumentExceptionIfNot(typeof(MenuTheme).IsAssignableFrom(themeType), nameof(themeType), $"{themeType} doesn't inherit {nameof(MenuTheme)}");

            ConstructorInfo ctor = themeType.GetConstructor(new[] { typeof(Menu) });
            Throw.InvalidOperationIf(ctor == null, $"{themeType} doesn't have a public constructor that takes a {nameof(Menu)}");

            return Theme = (MenuTheme)ctor.Invoke(new object[] { this });
        }

        public T SetTheme<T>() where T : MenuTheme
        {
            return (T)SetTheme(typeof(T));
        }

        public T CopyThemeFrom<T>(T themeTemplate) where T : MenuTheme
        {
            Throw.IfNull(themeTemplate, nameof(themeTemplate));

            T t = (T)themeTemplate.Clone(this);
            Theme = t;
            return t;
        }

        public MenuTheme CopyThemeFrom(Menu menu)
        {
            Throw.IfNull(menu, nameof(menu));
            
            return CopyThemeFrom(menu.Theme);
        }

        private int GetMinItemWithInputIndex() => GetMinItemIndexForCondition(m => m.IsVisible && !(m.IsDisabled && m.IsSkippedIfDisabled));
        private int GetMaxItemWithInputIndex() => GetMaxItemIndexForCondition(m => m.IsVisible && !(m.IsDisabled && m.IsSkippedIfDisabled));
        private int GetMinVisibleItemIndex() => GetMinItemIndexForCondition(m => m.IsVisible);
        private int GetMaxVisibleItemIndex() => GetMaxItemIndexForCondition(m => m.IsVisible);

        private int GetMinItemIndexForCondition(Predicate<MenuItem> condition)
        {
            int min = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                min = i;
                MenuItem item = Items[i];
                if (condition(item))
                    break;
            }

            return min;
        }

        private int GetMaxItemIndexForCondition(Predicate<MenuItem> condition)
        {
            int max = 0;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                max = i;
                MenuItem item = Items[i];
                if (condition(item))
                    break;
            }

            return max;
        }

        protected virtual MenuItemsCollection CreateItemsInstance()
        {
            return new MenuItemsCollection(this);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnSelectedItemChanged(SelectedItemChangedEventArgs e)
        {
            if (e.OldItem != null)
            {
                e.OldItem.PropertyChanged -= OnSelectedItemPropertyChanged;
            }

            if (e.NewItem != null)
            {
                e.NewItem.PropertyChanged += OnSelectedItemPropertyChanged;
            }

            SelectedItemChanged?.Invoke(this, e);
        }

        protected virtual void OnVisibleChanged(VisibleChangedEventArgs e)
        {
            VisibleChanged?.Invoke(this, e);
        }

        private void OnSelectedItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MenuItem.Description): UpdateCurrentDescription(); break;
            }
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    MenuManager.RemoveMenu(this);
                }

                IsDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }


    public class MenuControls
    {
        public Control PreviousItem { get; set; } = new Control(GameControl.FrontendUp);
        public Control NextItem { get; set; } = new Control(GameControl.FrontendDown);
        public Control ScrollerNextValue { get; set; } = new Control(GameControl.FrontendRight);
        public Control ScrollerPreviousValue { get; set; } = new Control(GameControl.FrontendLeft);
        public Control Accept { get; set; } = new Control(GameControl.FrontendAccept);
        public Control Back { get; set; } = new Control(GameControl.FrontendCancel);
    }


    // TODO: should MenuSoundsSet be part of MenuTheme?
    public class MenuSoundsSet
    {
        public FrontendSound Up { get; set; } = new FrontendSound("HUD_FRONTEND_DEFAULT_SOUNDSET", "NAV_UP_DOWN");
        public FrontendSound Down { get; set; } = new FrontendSound("HUD_FRONTEND_DEFAULT_SOUNDSET", "NAV_UP_DOWN");
        public FrontendSound Right { get; set; } = new FrontendSound("HUD_FRONTEND_DEFAULT_SOUNDSET", "NAV_LEFT_RIGHT");
        public FrontendSound Left { get; set; } = new FrontendSound("HUD_FRONTEND_DEFAULT_SOUNDSET", "NAV_LEFT_RIGHT");
        public FrontendSound Accept { get; set; } = new FrontendSound("HUD_FRONTEND_DEFAULT_SOUNDSET", "SELECT");
        public FrontendSound Back { get; set; } = new FrontendSound("HUD_FRONTEND_DEFAULT_SOUNDSET", "BACK");
        public FrontendSound Error { get; set; } = new FrontendSound("HUD_FRONTEND_DEFAULT_SOUNDSET", "ERROR");
    }
}

