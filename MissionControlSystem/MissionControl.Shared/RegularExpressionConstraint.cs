using System.Text.RegularExpressions;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Give the regular expression to validate a string <see cref="LaunchParameter"/>.
    /// </summary>
    public class RegularExpressionConstraint : Constraint, IEquatable<RegularExpressionConstraint>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public RegularExpressionConstraint()
        {
            Type = ConstraintType.RegularExpression;
        }

        /// <summary>
        /// The Regular Expression.
        /// </summary>
        public string RegularExpression { get; set; } = ".*";

        /// <summary>
        /// Error message to display if regular expression is not valid.
        /// </summary>
        public string ErrorMessage { get; set; } = "Value not valid";

        /// <inheritdoc/>
        public override bool Validate(object value)
        {
            var valueAsString = value.ToString();
            if (valueAsString == null)
            {
                return false;
            }
            return Regex.IsMatch(valueAsString, RegularExpression);
        }

        /// <inheritdoc/>
        public override Constraint DeepClone()
        {
            RegularExpressionConstraint ret = new();
            ret.RegularExpression = RegularExpression;
            ret.ErrorMessage = ErrorMessage;
            return ret;
        }

        public bool Equals(RegularExpressionConstraint? other)
        {
            return other != null && other.GetType() == typeof(RegularExpressionConstraint) &&
                RegularExpression == other.RegularExpression &&
                ErrorMessage == other.ErrorMessage;
        }

        protected override bool EqualsOfSameType(Constraint other)
        {
            return Equals((RegularExpressionConstraint)other);
        }
    }
}
