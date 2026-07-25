using System;

namespace Game.Core
{
    /// <summary>
    /// Represents success or a structured failure without an exception-based control path.
    /// </summary>
    public readonly struct Result
    {
        private Result(bool isSuccess, Error error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        /// <summary>
        /// Gets whether the operation succeeded.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the structured failure, or <see cref="Error.None"/> on success.
        /// </summary>
        public Error Error { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static Result Success()
        {
            return new Result(true, Error.None);
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static Result Failure(Error error)
        {
            if (!error.IsError)
            {
                throw new ArgumentException("Failure requires a non-empty error.", nameof(error));
            }

            return new Result(false, error);
        }
    }

    /// <summary>
    /// Represents a value-producing operation that can return a structured failure.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    public readonly struct Result<T>
    {
        private readonly T value;

        private Result(bool isSuccess, T value, Error error)
        {
            IsSuccess = isSuccess;
            this.value = value;
            Error = error;
        }

        /// <summary>
        /// Gets whether the operation succeeded.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the structured failure, or <see cref="Error.None"/> on success.
        /// </summary>
        public Error Error { get; }

        /// <summary>
        /// Gets the success value.
        /// </summary>
        /// <exception cref="InvalidOperationException">The result is a failure.</exception>
        public T Value
        {
            get
            {
                if (!IsSuccess)
                {
                    throw new InvalidOperationException(
                        "A failed result does not contain a value: " + Error);
                }

                return value;
            }
        }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static Result<T> Success(T successValue)
        {
            return new Result<T>(true, successValue, Error.None);
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static Result<T> Failure(Error error)
        {
            if (!error.IsError)
            {
                throw new ArgumentException("Failure requires a non-empty error.", nameof(error));
            }

            return new Result<T>(false, default, error);
        }

        /// <summary>
        /// Attempts to read the success value without throwing.
        /// </summary>
        public bool TryGetValue(out T successValue)
        {
            successValue = value;
            return IsSuccess;
        }
    }
}
