public class Forms
{
    private Gradient _a = new UnityEngine.Gradient();
    private Gradient _b = new Gradient { mode = GradientMode.Fixed };
    private Gradient _c = new();
    private void Trail(LineRenderer line) { line.endColor = Color.white; }
    private void Flat(LineRenderer line, ParticleSystem.MainModule main) { line.startColor = Color.white; main.startColor = Color.white; }
    private void Post() { var a = typeof(FilmGrain); var b = typeof(Bloom); }
    private object _d = new global::UnityEngine.Gradient();
    public Gradient Ramp { get; } = new();
    public Gradient Glow => new();
}
