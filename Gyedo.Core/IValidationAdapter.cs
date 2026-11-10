using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gyedo;

/// <summary>
/// 모델 타입별 검증 어댑터를 생성자로 주입하기 위한 인터페이스입니다.
/// </summary>
/// <remarks>
/// IModelValidator{T} 구현을 DI에 등록하고 ViewModel의 생성자에서 필요한 모델 타입을 요청합니다.
/// </remarks>
/// <typeparam name="T">Type of model being validated</typeparam>
public interface IModelValidator<in T> : IModelValidator
{
}

/// <summary>
/// Adapter used by ValidationModelBase to perform validation.
/// </summary>
/// <remarks>
/// This should be specialised to the particular ValidationModelBase instance it's validating
/// </remarks>
public interface IModelValidator
{
    /// <summary>
    /// Called by ValidatingModelBase, which passes in an instance of itself.
    /// This allows the IModelValidator to specialize to validating that particular ValidatingModelBase instance
    /// </summary>
    /// <param name="subject">Subject to initialize</param>
    void Initialize(object subject);

    /// <summary>
    /// Validate a single property by name, and return an array of validation errors for that property (or null if validation was successful)
    /// </summary>
    /// <param name="propertyName">Property to validate, or <see cref="string.Empty"/> to validate the entire model</param>
    /// <returns>Array of validation errors, or null / empty if validation was successful</returns>
    Task<IEnumerable<string>> ValidatePropertyAsync(string propertyName);

    /// <summary>
    /// Validate all properties, and return the results for all properties
    /// </summary>
    /// <remarks>
    /// Use a key of <see cref="string.Empty"/> to indicate validation errors for the entire model.
    /// 
    /// If a property validates successfully, you MUST return a null entry for it in the returned dictionary!
    /// </remarks>
    /// <returns>A dictionary of property name => array of validation errors (or null if that property validated successfully)</returns>
    Task<Dictionary<string, IEnumerable<string>>> ValidateAllPropertiesAsync();
}
