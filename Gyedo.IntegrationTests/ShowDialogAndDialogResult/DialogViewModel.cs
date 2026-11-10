using System.Threading;
using CommunityToolkit.Mvvm.Input;
using Gyedo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gyedo.IntegrationTests.ShowDialogAndDialogResult
{
    public partial class DialogViewModel : Screen
    {
        public BindableCollection<LabelledValue<bool>> DesiredResult { get; private set; }
        public LabelledValue<bool> SelectedDesiredResult { get; set; }

        public DialogViewModel()
        {
            this.DisplayName = "ShowDialog and DialogResult";

            this.DesiredResult = new BindableCollection<LabelledValue<bool>>()
            {
                new LabelledValue<bool>("True", true),
                new LabelledValue<bool>("False", false),
            };
            this.SelectedDesiredResult = this.DesiredResult[0];
        }

        [RelayCommand]
        public async Task Close()
        {
            await this.RequestCloseAsync(this.SelectedDesiredResult.Value);
        }
    }
}
