using temperatures.ViewModel;
using Microcharts;
using SkiaSharp;

namespace temperatures;

public partial class MainPage : ContentPage
{

    public MainPage(MainViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}

}

