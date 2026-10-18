namespace Bowling.App
{
    public class Boot
    {
        private void Wire()
        {
            var camera = FindObjectOfType<UnityEngine.Camera>();
            var ui = GameObject.Find("Ui");
            var data = Resources.Load("Data");
        }
    }
}
