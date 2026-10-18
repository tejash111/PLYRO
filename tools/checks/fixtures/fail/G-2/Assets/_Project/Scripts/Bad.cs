// no Vignette here
public class Bad
{
    private readonly Gradient _g = new Gradient();
    private void Fog() { RenderSettings.fog=true; }
    private void Cam() { var v = typeof(Vignette); } // uses Vignette
}
