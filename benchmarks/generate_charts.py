import pandas as pd
import matplotlib.pyplot as plt
import glob
import os

def generate_charts(results_path, output_dir):
    os.makedirs(output_dir, exist_ok=True)
    csv_files = glob.glob(os.path.join(results_path, "*-report.csv"))
    
    for file in csv_files:
        try:
            df = pd.read_csv(file)
            # Basic cleanup
            df = df.dropna(subset=['Mean'])
            
            # Clean 'Mean' column (remove units like 'ms', 'us', 'ns')
            def clean_value(val):
                if isinstance(val, str):
                    # Remove non-numeric parts but keep decimal point
                    numeric_part = ''.join(c for c in val if c.isdigit() or c == '.')
                    if numeric_part:
                        v = float(numeric_part)
                        if 'us' in val: return v / 1000
                        if 'ns' in val: return v / 1000000
                        return v
                return val

            df['Mean'] = df['Mean'].apply(clean_value)
            
            # If PageSize is present, create a grouped chart
            if 'PageSize' in df.columns:
                pivot_df = df.pivot(index='Method', columns='PageSize', values='Mean')
                pivot_df.plot(kind='bar', figsize=(12, 7), color=['#4a90e2', '#50e3c2', '#b8e986'])
                plt.title(f"Scaling Performance: {os.path.basename(file).split('-')[0]}")
                plt.ylabel("Execution Time")
                plt.yscale('log') # Use log scale for large scaling differences
            else:
                plt.figure(figsize=(10, 6))
                plt.bar(df['Method'], df['Mean'], color='#4a90e2')
                plt.title(f"Performance: {os.path.basename(file).split('-')[0]}")
                plt.ylabel("Execution Time")
            
            plt.xticks(rotation=45, ha='right')
            plt.tight_layout()
            
            chart_name = os.path.basename(file).replace("-report.csv", ".png")
            plt.savefig(os.path.join(output_dir, chart_name))
            plt.close()
            print(f"Generated chart: {chart_name}")
        except Exception as e:
            print(f"Failed to process {file}: {e}")

if __name__ == "__main__":
    import sys
    results_dir = sys.argv[1] if len(sys.argv) > 1 else "benchmarks/FlexQuery.Benchmarks/BenchmarkDotNet.Artifacts/results"
    images_dir = "docs/performance/images"
    generate_charts(results_dir, images_dir)
