namespace MauiApp1.Pages;

public partial class MyMExpense : ContentPage
{
    private int count = 0;
    public MyMExpense()
	{
		InitializeComponent();
	}

    private void Button_Clicked(object sender, EventArgs e)
    {
        count++;
        lblCount.Text = $"Count: {count}";
    }
}