using System.Text.Json;
using RiftReview.Core.Results;

namespace RiftReview.Capture;

/// <summary>Where League's replay camera is: position, rotation (degrees) and field of view.</summary>
public sealed record CameraView(double X, double Y, double Z, double Pitch, double Yaw, double Roll, double FieldOfView);

/// <summary>
/// The parts of <c>/replay/render</c> Rift Review reads: the selected player (League's target
/// frame; their Riot ID game name, empty for none) and the camera.
/// </summary>
public sealed record RenderState(string SelectionName, CameraView Camera);

/// <summary>Parses the replay API's <c>/replay/render</c> JSON.</summary>
public static class RenderStateParser
{
    /// <summary>The selection and camera, or why they could not be read.</summary>
    public static Result<RenderState, string> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("cameraPosition", out var position) || !root.TryGetProperty("cameraRotation", out var rotation))
            {
                return Result<RenderState, string>.Failure("render has no cameraPosition or cameraRotation");
            }

            var selection = root.TryGetProperty("selectionName", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString()! : string.Empty;
            var fieldOfView = root.TryGetProperty("fieldOfView", out var fov) && fov.TryGetDouble(out var f) ? f : 40;
            return Result<RenderState, string>.Success(new RenderState(
                selection,
                new CameraView(Axis(position, "x"), Axis(position, "y"), Axis(position, "z"), Axis(rotation, "x"), Axis(rotation, "y"), Axis(rotation, "z"), fieldOfView)));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return Result<RenderState, string>.Failure($"render is not readable: {exception.Message}");
        }
    }

    private static double Axis(JsonElement vector, string axis) => vector.GetProperty(axis).GetDouble();
}
