using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace RevitBridge.Addin.UI
{
    public class ApprovalService
    {
        /// <summary>
        /// Solicita aprobación mostrando una ventana modeless.
        /// Devuelve un Task que se completa con true (aprobado) o false (rechazado).
        /// </summary>
        /// <param name="snippet">Código a mostrar en la ventana</param>
        /// <returns>True si es aprobado, False si es rechazado o si caduca</returns>
        public bool SolicitarAprobacion(string snippet)
        {
            var window = new ApprovalWindow(snippet);
            window.ShowDialog();
            return window.IsApproved;
        }
    }
}
