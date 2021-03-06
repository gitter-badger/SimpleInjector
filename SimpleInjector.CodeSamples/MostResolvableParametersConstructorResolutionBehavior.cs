﻿namespace SimpleInjector.CodeSamples
{
    // https://simpleinjector.codeplex.com/discussions/353520
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using SimpleInjector.Advanced;

    // Mimics the constructor resolution behavior of Autofac, Ninject and Castle Windsor.
    // Register this as follows:
    // container.Options.ConstructorResolutionBehavior =
    //     new MostResolvableParametersConstructorResolutionBehavior(container);
    public class MostResolvableParametersConstructorResolutionBehavior : IConstructorResolutionBehavior
    {
        private readonly Container container;

        public MostResolvableParametersConstructorResolutionBehavior(Container container)
        {
            this.container = container;
        }

        private bool IsCalledDuringRegistrationPhase
        {
            [DebuggerStepThrough]
            get { return !this.container.IsLocked(); }
        }

        [DebuggerStepThrough]
        public ConstructorInfo GetConstructor(Type serviceType, Type implementationType)
        {
            var constructor = this.GetConstructorOrNull(serviceType, implementationType);

            if (constructor != null)
            {
                return constructor;
            }

            throw new ActivationException(BuildExceptionMessage(implementationType));
        }

        [DebuggerStepThrough]
        private ConstructorInfo GetConstructorOrNull(Type serviceType, Type implementationType)
        {
            // We prevent calling GetRegistration during the registration phase, because at this point not
            // all dependencies might be registered, and calling GetRegistration would lock the container,
            // making it impossible to do other registrations.
            return (
                from ctor in implementationType.GetConstructors()
                let parameters = ctor.GetParameters()
                orderby parameters.Length descending
                where this.IsCalledDuringRegistrationPhase || 
                    parameters.All(parameter => this.CanBeResolved(serviceType, implementationType, parameter))
                select ctor)
                .FirstOrDefault();
        }

        [DebuggerStepThrough]
        private bool CanBeResolved(Type serviceType, Type implementationType, ParameterInfo parameter)
        {
            return this.container.GetRegistration(parameter.ParameterType) != null ||
                this.CanBuildParameterExpression(serviceType, implementationType, parameter);
        }

        [DebuggerStepThrough]
        private bool CanBuildParameterExpression(Type serviceType, Type implementationType, ParameterInfo parameter)
        {
            try
            {
                this.container.Options.ConstructorInjectionBehavior.BuildParameterExpression(
                    serviceType, implementationType, parameter);

                return true;
            }
            catch (ActivationException)
            {
                return false;
            }
        }

        [DebuggerStepThrough]
        private static string BuildExceptionMessage(Type type)
        {
            if (!type.GetConstructors().Any())
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "For the container to be able to create {0}, it should contain at least one public " +
                    "constructor.", type);
            }

            return string.Format(CultureInfo.InvariantCulture,
                "For the container to be able to create {0}, it should contain a public constructor that " +
                "only contains parameters that can be resolved.", type);
        }
    }
}