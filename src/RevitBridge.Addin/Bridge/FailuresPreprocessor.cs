using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitBridge.Addin.Bridge
{
    public class FailuresPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            IList<FailureMessageAccessor> fmas = failuresAccessor.GetFailureMessages();
            if (fmas.Count == 0)
            {
                return FailureProcessingResult.Continue;
            }
            
            bool hasErrors = false;
            foreach (FailureMessageAccessor fma in fmas)
            {
                FailureSeverity severity = fma.GetSeverity();
                if (severity == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(fma);
                }
                else if (severity == FailureSeverity.Error)
                {
                    hasErrors = true;
                }
            }

            if (hasErrors)
            {
                return FailureProcessingResult.ProceedWithRollBack;
            }

            return FailureProcessingResult.Continue;
        }
    }
}
