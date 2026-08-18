using System.Collections.Generic;
using RevitBridge.Core;
using Xunit;

namespace RevitBridge.Tests.Core;

/// <summary>
/// F2.5 (§5.C.10/§5.D.15): decisión centralizada de si hace falta aprobación. Extraída a Core
/// tras la auditoría 2026-08-18, donde un comando (ParamCommands.ModificarParametroTextoCategoria)
/// se quedó sin esta comprobación por reimplementarla a mano en vez de compartirla.
/// </summary>
public class PreexistingElementGuardTests
{
    [Fact]
    public void NoRequiereAprobacion_Si_Todos_Los_Ids_Son_De_La_Sesion()
    {
        var creadosEnSesion = new HashSet<long> { 1, 2, 3 };

        var requiere = PreexistingElementGuard.RequiereAprobacion(new long[] { 1, 2 }, creadosEnSesion);

        Assert.False(requiere);
    }

    [Fact]
    public void RequiereAprobacion_Si_Al_Menos_Un_Id_Es_Preexistente()
    {
        var creadosEnSesion = new HashSet<long> { 1, 2 };

        var requiere = PreexistingElementGuard.RequiereAprobacion(new long[] { 1, 99 }, creadosEnSesion);

        Assert.True(requiere);
    }

    [Fact]
    public void RequiereAprobacion_Si_La_Sesion_No_Ha_Creado_Nada_Todavia()
    {
        var creadosEnSesion = new HashSet<long>();

        var requiere = PreexistingElementGuard.RequiereAprobacion(new long[] { 42 }, creadosEnSesion);

        Assert.True(requiere);
    }

    [Fact]
    public void NoRequiereAprobacion_Si_La_Lista_De_Afectados_Esta_Vacia()
    {
        var creadosEnSesion = new HashSet<long> { 1 };

        var requiere = PreexistingElementGuard.RequiereAprobacion(System.Array.Empty<long>(), creadosEnSesion);

        Assert.False(requiere);
    }
}
