// El Dispatcher de Avalonia es un singleton ligado al primer hilo que lo toca.
// Se desactiva la paralelización para que las pruebas headless (UI) no compitan
// con pruebas que invocan Dispatcher.UIThread desde hilos del pool.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
