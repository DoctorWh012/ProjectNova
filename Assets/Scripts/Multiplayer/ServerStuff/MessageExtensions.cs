using Riptide;
using Unity.Mathematics;
using UnityEngine;

public static class MessageExtensions
{
    #region Vector2
    /// <inheritdoc cref="Add(Message, Vector2)"/>
    /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(Message, Vector2)"/> and simply provides an alternative type-explicit way to add a <see cref="Vector2"/> to the message.</remarks>
    public static Message AddVector2(this Message message, Vector2 value) => Add(message, value);

    /// <summary>Adds a <see cref="Vector2"/> to the message.</summary>
    /// <param name="value">The <see cref="Vector2"/> to add.</param>
    /// <returns>The message that the <see cref="Vector2"/> was added to.</returns>
    public static Message Add(this Message message, Vector2 value)
    {
        message.AddFloat(value.x);
        message.AddFloat(value.y);
        return message;
    }

    /// <summary>Retrieves a <see cref="Vector2"/> from the message.</summary>
    /// <returns>The <see cref="Vector2"/> that was retrieved.</returns>
    public static Vector2 GetVector2(this Message message)
    {
        return new Vector2(message.GetFloat(), message.GetFloat());
    }
    #endregion

    #region Vector3
    /// <inheritdoc cref="Add(Message, Vector3)"/>
    /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(Message, Vector3)"/> and simply provides an alternative type-explicit way to add a <see cref="Vector3"/> to the message.</remarks>
    public static Message AddVector3(this Message message, Vector3 value) => Add(message, value);

    /// <summary>Adds a <see cref="Vector3"/> to the message.</summary>
    /// <param name="value">The <see cref="Vector3"/> to add.</param>
    /// <returns>The message that the <see cref="Vector3"/> was added to.</returns>
    public static Message Add(this Message message, Vector3 value)
    {
        message.AddFloat(value.x);
        message.AddFloat(value.y);
        message.AddFloat(value.z);
        return message;
    }

    /// <summary>Retrieves a <see cref="Vector3"/> from the message.</summary>
    /// <returns>The <see cref="Vector3"/> that was retrieved.</returns>
    public static Vector3 GetVector3(this Message message)
    {
        return new Vector3(message.GetFloat(), message.GetFloat(), message.GetFloat());
    }

    /// <summary>Adds an array of <see cref="Vector3"/> to the message.</summary>
    /// <param name="values">The Array of <see cref="Vector3"/> to add.</param>
    /// <returns>The message that the array of <see cref="Vector3"/> was added to.</returns>
    public static Message AddVector3s(this Message message, Vector3[] values)
    {
        message.AddInt(values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            message.AddFloat(values[i].x);
            message.AddFloat(values[i].y);
            message.AddFloat(values[i].z);
        }
        return message;
    }

    /// <summary>Retrieves an Array of <see cref="Vector3"/> from the message.</summary>
    /// <returns>The array of <see cref="Vector3"/> that was retrieved.</returns>
    public static Vector3[] GetVector3s(this Message message)
    {
        int length = message.GetInt();

        Vector3[] values = new Vector3[length];
        for (int i = 0; i < length; i++) values[i] = message.GetVector3();
        return values;
    }

    /// <summary>Adds an array of <see cref="Vector3"/> to the message.</summary>
    /// <param name="values">The Array of <see cref="Vector3"/> to add.</param>
    /// <returns>The message that the array of <see cref="Vector3"/> was added to.</returns>
    public static Message AddQuaternions(this Message message, Quaternion[] values)
    {
        message.AddInt(values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            message.AddFloat(values[i].x);
            message.AddFloat(values[i].y);
            message.AddFloat(values[i].z);
            message.AddFloat(values[i].w);
        }
        return message;
    }

    /// <summary>Retrieves an Array of <see cref="Vector3"/> from the message.</summary>
    /// <returns>The array of <see cref="Vector3"/> that was retrieved.</returns>
    public static Quaternion[] GetQuaternions(this Message message)
    {
        int length = message.GetInt();

        Quaternion[] values = new Quaternion[length];
        for (int i = 0; i < length; i++) values[i] = message.GetQuaternion();
        return values;
    }
    #endregion

    #region Quaternion
    /// <inheritdoc cref="Add(Message, Quaternion)"/>
    /// <remarks>Relying on the correct Add overload being chosen based on the parameter type can increase the odds of accidental type mismatches when retrieving data from a message. This method calls <see cref="Add(Message, Quaternion)"/> and simply provides an alternative type-explicit way to add a <see cref="Quaternion"/> to the message.</remarks>
    public static Message AddQuaternion(this Message message, Quaternion value) => Add(message, value);

    /// <summary>Adds a <see cref="Quaternion"/> to the message.</summary>
    /// <param name="value">The <see cref="Quaternion"/> to add.</param>
    /// <returns>The message that the <see cref="Quaternion"/> was added to.</returns>
    public static Message Add(this Message message, Quaternion value)
    {
        message.AddFloat(value.x);
        message.AddFloat(value.y);
        message.AddFloat(value.z);
        message.AddFloat(value.w);
        return message;
    }

    /// <summary>Retrieves a <see cref="Quaternion"/> from the message.</summary>
    /// <returns>The <see cref="Quaternion"/> that was retrieved.</returns>
    public static Quaternion GetQuaternion(this Message message)
    {
        return new Quaternion(message.GetFloat(), message.GetFloat(), message.GetFloat(), message.GetFloat());
    }
    #endregion
}