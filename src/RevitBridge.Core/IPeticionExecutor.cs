namespace RevitBridge.Core;

/// <summary>
/// Costura de abstracción entre el transporte (<c>PipeServer</c>, hilo del listener) y la
/// ejecución real (<c>ExecutionQueue</c> vía <c>ExternalEvent</c>, F0.5). El servidor de pipe solo
/// conoce esta interfaz: no sabe nada de <c>ExternalEvent</c> ni de la API de Revit, así que se
/// testea con un ejecutor falso (specs/tech-spec.md §Testing Strategy, fila PipeServer).
/// </summary>
public interface IPeticionExecutor
{
    /// <summary>
    /// Resuelve una petición completa del pipe y devuelve la respuesta ya definitiva. No debe
    /// devolver "aceptado" antes de tener el resultado real (DOCUMENTACION.md §2).
    /// </summary>
    Task<RespuestaOperacion> EjecutarAsync(PeticionPipe peticion, CancellationToken cancellationToken = default);
}
