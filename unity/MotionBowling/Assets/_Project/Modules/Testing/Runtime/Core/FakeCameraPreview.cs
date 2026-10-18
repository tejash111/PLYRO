using Bowling.Contracts;
using UnityEngine;

namespace Bowling.Testing
{
    // An ICameraPreview whose texture a test sets. Starts with no texture (null). The fake does not
    // own the texture: the test that creates it destroys it.
    public sealed class FakeCameraPreview : ICameraPreview
    {
        public Texture Texture { get; private set; }

        public void Set(Texture texture)
        {
            Texture = texture;
        }
    }
}
