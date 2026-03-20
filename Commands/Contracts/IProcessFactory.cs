namespace AWSS3Zip.Commands.Contracts;

public interface IProcessFactory<T> where T : IProcessJob
{
    public IProcessFactory<T> Build(string[] parameters);
    public void Execute();
}
