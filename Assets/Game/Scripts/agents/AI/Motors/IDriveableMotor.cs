// Input-driven continuous steering channel for motors. Parallels IMovementMotor (the AI goal
// channel) but carries raw per-frame input instead of a goal: whoever is driving the body forwards
// one of these each frame, and the motor interprets it in its own physics model.
//
// A motor that implements IDriveableMotor must stamp Time.frameCount inside ApplyDriveInput and,
// in its Tick(MoveIntent) implementation, skip MoveIntent interpretation on the same frame so the
// AI channel cannot fight the driver. Arc/cooldown updates should still run.
using UnityEngine;

namespace SpaceGame.Agents
{
    public readonly struct DriveInput
    {
        // x = yaw (turn left/right), y = throttle (forward/back). Already smoothed by the caller.
        public readonly Vector2 Move;
        // Ascend/descend axis. Ground motors ignore it.
        public readonly float Vertical;
        // The driver asked for a "running" speed (sprint) this frame.
        public readonly bool IsRunning;
        // Dedicated yaw axis, -1..1, for a machine whose Move.x means something other than turning.
        // A lateral traveller spends Move.x on strafe and steers from this instead; every machine
        // that turns with Move.x ignores it, and it stays 0 when no turn action is bound.
        public readonly float Turn;

        public DriveInput(Vector2 move, float vertical, bool isRunning)
            : this(move, vertical, isRunning, 0f) { }

        public DriveInput(Vector2 move, float vertical, bool isRunning, float turn)
        {
            Move = move;
            Vertical = vertical;
            IsRunning = isRunning;
            Turn = turn;
        }
    }

    public interface IDriveableMotor
    {
        void ApplyDriveInput(in DriveInput input, float deltaTime);
    }

    /// <summary>Optional motor extension for a body that can jump.</summary>
    public interface IJumpMotor
    {
        void RequestJump();
    }

    /// <summary>A long horizontal dash with a vertical arc.</summary>
    public interface ILeapMotor
    {
        bool IsLeapAvailable { get; }
        bool IsLeaping { get; }
        void RequestLeap(Vector3 direction, float horizontalDistance, float verticalHeight, float duration);
    }
}
