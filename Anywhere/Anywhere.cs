using System.Linq.Expressions;

namespace Anywhere
{
    public class Anywhere
    {
        // TODO configure remote or local. remote needs some kind of connection string
        public Anywhere()
        {

        }

        public async Task<Tprop> Execute<Tprop>(Expression<Func<Tprop>> expression)
        {
            // TODO: change behavior whether local or remote execution
            return await LocalExecute<Tprop>(expression);
        }

        // TODO: maybe only allow static method expressions to better enforce good usage patterns and simplify serialization (since then all arguments need to be simple)?
        public async Task<Tprop> LocalExecute<Tprop>(Expression<Func<Tprop>> expression)
        {
            try
            {
                var data = MethodModelBuilder.Serialize(expression);
                var result = MethodModelBuilder.DeserializeAndExecute<Tprop>(data);
                return result;
            }
            catch (Exception e)
            {
                throw;
            }
        }

        // TODO: add remote execution: convenience wrapper to block until remote execution completes
        public async Task<Tprop> RemoteExecute<Tprop>(Expression<Func<Tprop>> expression)
        {
            var data = MethodModelBuilder.Serialize(expression);


            var result = MethodModelBuilder.DeserializeAndExecute<Tprop>(data);
            return result;
        }
    }
}
