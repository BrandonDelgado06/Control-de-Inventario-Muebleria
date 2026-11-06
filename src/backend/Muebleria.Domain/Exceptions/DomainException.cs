namespace Muebleria.Domain.Exceptions;

/// <summary>Regla de negocio incumplida. La API la devuelve como HTTP 400 con este mensaje.</summary>
public class DomainException(string mensaje) : Exception(mensaje);

public class StockInsuficienteException(string articulo, decimal disponible, decimal solicitado)
    : DomainException($"No hay suficiente existencia de \"{articulo}\": disponible {disponible:0.##}, solicitado {solicitado:0.##}.");

/// <summary>La API la devuelve como HTTP 404.</summary>
public class NoEncontradoException(string entidad, object id)
    : Exception($"{entidad} con id {id} no existe.");
