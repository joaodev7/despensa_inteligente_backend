namespace DespensaInteligente.Application.InvoiceScanner.Common;

/// <summary>
/// Result Pattern para retorno tipado sem lançamento desnecessário de exceções na camada de Aplicação.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public Error Error { get; }

    protected Result(bool isSuccess, T? value, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Um resultado de sucesso não pode conter um erro.");

        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Um resultado de falha precisa conter um erro válido.");

        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, Error.None);
    public static Result<T> Failure(Error error) => new(false, default, error);
}

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
}
