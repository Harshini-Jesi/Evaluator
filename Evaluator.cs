namespace Eval;

class EvalException : Exception {
   public EvalException (string message) : base (message) { }
}

class Evaluator {
   public double Evaluate (string text) {
      Reset ();
      var tokenizer = new Tokenizer (this, text);
      for (; ; ) {
         var token = tokenizer.Next ();
         if (token is TEnd) break;
         if (token is TError err) throw new EvalException (err.Message);
         mTokens.Add (token);
      }

      // Check if this is a variable assignment
      TVariable? tVariable = null;
      if (mTokens.Count > 2 && mTokens[0] is TVariable tvar && mTokens[1] is TOpArithmetic { Op: '=' }) {
         tVariable = tvar;
         mTokens.RemoveRange (0, 2);
      }
      foreach (var t in mTokens) Process (t);
      while (mOperators.Count > 0) ApplyOperator ();
      if (mOperators.Count > 0) throw new EvalException ("Too many operators");
      if (mOperands.Count > 1) throw new EvalException ("Too many operands");
      if (BasePriority != 0) throw new EvalException ("Invalid Paranthesis");
      double f = mOperands.Pop ();
      if (tVariable != null) mVars[tVariable.Name] = f;
      return f;
   }

   public int BasePriority { get; private set; }

   public double GetVariable (string name) {
      if (mVars.TryGetValue (name, out double f)) return f;
      throw new EvalException ($"Unknown variable: {name}");
   }
   readonly Dictionary<string, double> mVars = new ();

   void Process (Token token) {
      switch (token) {
         case TNumber num:
            mOperands.Push (num.Value);
            break;
         case TOperator op:
            op.FinalPriority = BasePriority + op.Priority;
            while (!OkToPush (op))
               ApplyOperator ();
            mOperators.Push (op);
            break;
         case TPunctuation p:
            BasePriority += p.Punct == '(' ? 10 : -10;
            break;
         default:
            throw new EvalException ($"Unknown token: {token}");
      }

      bool OkToPush (TOperator op) => mOperators.Count == 0 || mOperators.Peek ().FinalPriority < op.FinalPriority || op is TOpUnary;
   }
   readonly Stack<double> mOperands = new ();
   readonly Stack<TOperator> mOperators = new ();

   void ApplyOperator () {
      var op = mOperators.Pop ();
      var f1 = mOperands.Pop ();
      switch (op) {
         case TOpFunction func:
            mOperands.Push (func.Evaluate (f1));
            break;
         case TOpArithmetic arith:
            if (mOperands.Count < 1) throw new EvalException ("Too few operands");
            var f2 = mOperands.Pop ();
            mOperands.Push (arith.Evaluate (f2, f1));
            break;
         case TOpUnary unary:
            mOperands.Push (unary.Evaluate (f1));
            break;
      }
   }

   void Reset () {
      mOperators.Clear (); mOperands.Clear (); mTokens.Clear (); BasePriority = 0;
   }

   public Token GetPrevToken () => mTokens[^1];

   readonly List<Token> mTokens = new ();
}
