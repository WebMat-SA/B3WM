using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace B3WM.Shared.Extensions
{
    public class PasswordValidator : ValidationAttribute
    {
        private string RegularExp { get; set; }
        public PasswordValidator(string _RegexExp) { RegularExp = _RegexExp; }

        public override bool IsValid(object value)
        {
            if (!string.IsNullOrEmpty(RegularExp))
            {
                Regex regex = new Regex(RegularExp);

                var isMatch = regex.IsMatch(value as string);

                if (!isMatch)
                {
                    ErrorMessage = "O Campo deve conter ao menos 6 digitos, sendo uma letra maiúscula, uma minúscula e um numeral.";
                    return false;
                }

                return true;
            }

            return false;
        }
    }

    public class MustMatchValidator : ValidationAttribute
    {
        private string NameProperty { get; set; }

        public MustMatchValidator(string nameProperty)
        {
            NameProperty = nameProperty;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            ErrorMessage = ErrorMessageString;
            if (value == null)
                return new ValidationResult(ErrorMessage,new[] { validationContext.MemberName });

            var currentValue = (string)value;

            var property = validationContext.ObjectType.GetProperty(NameProperty);

            if (property == null)
                throw new ArgumentException("Property with this name not found");

            var objValue = property.GetValue(validationContext.ObjectInstance);

            if (objValue == null)
                return new ValidationResult(ErrorMessage, new[] { validationContext.MemberName });

            var comparisonValue = (string)objValue;

            if (!currentValue.Equals(comparisonValue))
                return new ValidationResult(ErrorMessage, new[] { validationContext.MemberName});
                

            return ValidationResult.Success;
        }
    }

    public class RequiredIfValidator : ValidationAttribute
    {
        RequiredAttribute _innerAttribute = new RequiredAttribute();
        public string _dependentProperty { get; set; }
        public object _targetValue { get; set; }
        public TypeCondition _typeCondition { get; set; }

        public RequiredIfValidator(string dependentProperty, object targetValue, TypeCondition typeCondition)
        {
            this._dependentProperty = dependentProperty;
            this._targetValue = targetValue;
            this._typeCondition = typeCondition;
        }
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var field = validationContext.ObjectType.GetProperty(_dependentProperty);
            if (field != null)
            {
                var dependentValue = field.GetValue(validationContext.ObjectInstance, null);

                if (_typeCondition == TypeCondition.Equal)
                {
                    if ((dependentValue == null && _targetValue == null) || (dependentValue.Equals(_targetValue)))
                    {
                        if (!_innerAttribute.IsValid(value))
                        {
                            return new ValidationResult(ErrorMessage = "Campo Obrigatório.", new[] { validationContext.MemberName });
                        }
                    }
                }else if (_typeCondition == TypeCondition.Diff)
                {
                    if (!(dependentValue == null && _targetValue == null) && (!dependentValue.Equals(_targetValue)))
                    {
                        if (!_innerAttribute.IsValid(value))
                        {
                            return new ValidationResult(ErrorMessage = "Campo Obrigatório.", new[] { validationContext.MemberName });
                        }
                    }
                }
                return ValidationResult.Success;
            }
            else
            {
                return new ValidationResult(FormatErrorMessage(_dependentProperty), new[] { validationContext.MemberName });
            }
        }

        public enum TypeCondition
        {
            Equal,
            Diff
        }
    }

}
