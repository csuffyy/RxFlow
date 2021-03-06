﻿using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace RxFlow
{
    public static class ReactiveHelper
    {
        public static IObservable<T> Retry<T>(this IObservable<T> source, int retryCount, TimeSpan delaySpan)
        {
            return source.Retry<T, Exception>(retryCount, null, delaySpan, Scheduler.Default);
        }

        public static IObservable<T> Retry<T, TException>(this IObservable<T> source, int retryCount,
            Action<TException> exAction, TimeSpan delaySpan)
            where TException : Exception
        {
            return source.Retry(retryCount, exAction, delaySpan, Scheduler.Default);
        }

        public static IObservable<T> Retry<T, TException>(this IObservable<T> source, int retryCount,
            Action<TException> exAction, TimeSpan delaySpan, IScheduler scheduler)
            where TException : Exception
        {
            return source.Catch((TException ex) =>
            {
                if (exAction != null) exAction(ex);
                if (retryCount == 1) return Observable.Throw<T>(ex);

                if (retryCount <= 0)
                    return
                        Observable.Timer(delaySpan, scheduler)
                            .SelectMany(_ => source.Retry(retryCount, exAction, delaySpan, scheduler));

                var nowRetryCount = 1;

                return
                    Observable.Timer(delaySpan, scheduler)
                        .SelectMany(_ => source.Retry(retryCount, exAction, delaySpan, scheduler, nowRetryCount));
            });
        }

        private static IObservable<T> Retry<T, TException>(this IObservable<T> source, int retryCount,
            Action<TException> exAction, TimeSpan delaySpan, IScheduler scheduler, int nowRetryCount)
            where TException : Exception
        {
            return source.Catch((TException ex) =>
            {
                nowRetryCount++;

                if (exAction != null) exAction(ex);

                if (nowRetryCount < retryCount)
                    return
                        Observable.Timer(delaySpan, scheduler)
                            .SelectMany(_ => source.Retry(retryCount, exAction, delaySpan, scheduler, nowRetryCount));

                return Observable.Throw<T>(ex);
            });
        }

        public static IObservable<T> While<T>(this IObservable<T> source, Func<bool> condition)
        {
            return WhileCore(condition, source).Concat();
        }

        public static IObservable<T> DoWhile<T>(this IObservable<T> source, Func<bool> condition)
        {
            return source.Concat(source.While(condition));
        }

        private static IEnumerable<IObservable<T>> WhileCore<T>(Func<bool> condition, IObservable<T> source)
        {
            while (condition()) yield return source;
        }

        public static IObservable<T> Junction<T>(this IObservable<T> source, IObserver<T> branch)
        {
            return Junction(source, _ => true, _ => _, branch);
        }

        public static IObservable<T> Junction<T>(this IObservable<T> source, Func<T, bool> branchSelector,
            IObserver<T> branch)
        {
            return Junction(source, branchSelector, _ => _, branch);
        }

        public static IObservable<TIn> Junction<TIn, TOut>(this IObservable<TIn> source, Func<TIn, TOut> converter,
            IObserver<TOut> branch)
        {
            return Junction(source, _ => true, converter, branch);
        }

        public static IObservable<TIn> Junction<TIn, TOut>(this IObservable<TIn> source, Func<TIn, bool> branchSelector,
            Func<TIn, TOut> converter, IObserver<TOut> branch)
        {
            return new AnonymousObservable<TIn>(observer =>
            {
                return source.Subscribe(new AnonymousObserver<TIn>(value =>
                {
                    if (branchSelector(value))
                    {
                        branch.OnNext(converter(value));
                    }
                    else
                    {
                        observer.OnNext(value);
                    }
                },
                    ex =>
                    {
                        branch.OnError(ex);
                        observer.OnError(ex);
                    },
                    () =>
                    {
                        branch.OnCompleted();
                        observer.OnCompleted();
                    }));
            });
        }

        public static IObservable<T> Distribution<T>(this IObservable<T> source, IObserver<T> branch)
        {
            return Distribution(source, _ => true, _ => _, branch);
        }

        public static IObservable<T> Distribution<T>(this IObservable<T> source, Func<T, bool> branchSelector,
            IObserver<T> branch)
        {
            return Distribution(source, branchSelector, _ => _, branch);
        }

        public static IObservable<TIn> Distribution<TIn, TOut>(this IObservable<TIn> source, Func<TIn, TOut> converter,
            IObserver<TOut> branch)
        {
            return Distribution(source, _ => true, converter, branch);
        }

        public static IObservable<TIn> Distribution<TIn, TOut>(this IObservable<TIn> source,
            Func<TIn, bool> branchSelector, Func<TIn, TOut> converter, IObserver<TOut> branch)
        {
            return new AnonymousObservable<TIn>(observer =>
            {
                return source.Subscribe(new AnonymousObserver<TIn>(value =>
                {
                    if (branchSelector(value))
                    {
                        branch.OnNext(converter(value));
                    }
                    observer.OnNext(value);
                },
                    ex =>
                    {
                        branch.OnError(ex);
                        observer.OnError(ex);
                    },
                    () =>
                    {
                        branch.OnCompleted();
                        observer.OnCompleted();
                    }));
            });
        }
    }
}