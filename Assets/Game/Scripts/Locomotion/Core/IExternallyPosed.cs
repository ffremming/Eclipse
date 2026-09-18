// A kinematic layer that normally OWNS its body's transform, but can be told to stop writing it
// and read it instead.
//
// LeggedLocomotion is invariant I4: the single owner of the body's transform. That is right while
// the locomotion is the thing deciding where the machine goes. When something else is placing the
// body -- a rig being posed, a body carried by something bigger -- a locomotion that keeps writing
// its own answer in LateUpdate wins that argument every frame, and the walker stands still wherever
// it started while the thing carrying it moves away.
//
// Switching the locomotion OFF is not the alternative: it both moves the body and solves the legs,
// so a disabled one leaves the body sliding along with still feet. Following is the only option
// that gets both halves right.
//
namespace SpaceGame.Locomotion
{
    public interface IExternallyPosed
    {
        /// <summary>
        /// False (the default) to own the body's transform. True to leave it to whatever else is
        /// writing it and derive this frame's motion by measuring the result.
        /// </summary>
        bool ExternallyPosed { get; set; }
    }
}
