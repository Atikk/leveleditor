#!/usr/bin/env python3
"""Summarise telemetry exports produced by collect-job-system-telemetry.ps1."""

from __future__ import annotations

import argparse
import csv
import json
import statistics
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple


@dataclass
class JobSample:
    pending_jobs: int
    active_workers: int
    completed_jobs: int
    configured_workers: int


@dataclass
class Summary:
    samples: int
    pending_avg: float
    pending_max: int
    active_avg: float
    active_peak: int
    configured_workers: int
    completed_final: int

    def to_dict(self) -> Dict[str, object]:
        return {
            "samples": self.samples,
            "pendingAvg": round(self.pending_avg, 3),
            "pendingMax": self.pending_max,
            "activeAvg": round(self.active_avg, 3),
            "activePeak": self.active_peak,
            "configuredWorkers": self.configured_workers,
            "completedFinal": self.completed_final,
        }


def parse_csv(path: Path) -> Dict[str, List[JobSample]]:
    with path.open(newline="", encoding="utf-8") as handle:
        reader = csv.DictReader(handle)
        samples: Dict[str, List[JobSample]] = {}
        for row in reader:
            name = row["jobSystem"].strip()
            bucket = samples.setdefault(name, [])
            bucket.append(
                JobSample(
                    pending_jobs=int(row["pendingJobs"]),
                    active_workers=int(row["activeWorkers"]),
                    completed_jobs=int(row["completedJobs"]),
                    configured_workers=int(row["configuredWorkers"]),
                )
            )
    return samples


def summarise(samples: Iterable[JobSample]) -> Optional[Summary]:
    data = list(samples)
    if not data:
        return None

    pending_values = [sample.pending_jobs for sample in data]
    active_values = [sample.active_workers for sample in data]
    configured_workers = data[-1].configured_workers
    completed_final = data[-1].completed_jobs

    return Summary(
        samples=len(data),
        pending_avg=statistics.fmean(pending_values) if pending_values else 0.0,
        pending_max=max(pending_values) if pending_values else 0,
        active_avg=statistics.fmean(active_values) if active_values else 0.0,
        active_peak=max(active_values) if active_values else 0,
        configured_workers=configured_workers,
        completed_final=completed_final,
    )


def discover_job_csvs(root: Path) -> Dict[str, Path]:
    outputs: Dict[str, Path] = {}
    for csv_path in root.rglob("*.csv"):
        if csv_path.name.lower().endswith("job-systems.csv") or csv_path.stem.lower().startswith("job-system"):
            outputs[csv_path.parent.name] = csv_path
    return outputs


def analyse_directory(directory: Path) -> Dict[str, Dict[str, Summary]]:
    sessions: Dict[str, Dict[str, Summary]] = {}
    for session_dir in directory.iterdir():
        if not session_dir.is_dir():
            continue
        csv_map = discover_job_csvs(session_dir)
        if not csv_map:
            continue
        session_results: Dict[str, Summary] = {}
        for _, csv_path in csv_map.items():
            sample_groups = parse_csv(csv_path)
            for job_name, samples in sample_groups.items():
                summary = summarise(samples)
                if summary is not None:
                    session_results[job_name] = summary
        if session_results:
            sessions[session_dir.name] = session_results
    return sessions


def format_summary(data: Dict[str, Dict[str, Summary]]) -> str:
    lines: List[str] = []
    for session, jobs in sorted(data.items()):
        lines.append(f"Session: {session}")
        for job_name, summary in sorted(jobs.items()):
            metrics = summary.to_dict()
            lines.append(
                "  {name}: samples={samples}, pending(avg={pendingAvg}, max={pendingMax}), "
                "active(avg={activeAvg}, peak={activePeak}), configured={configuredWorkers}, "
                "completedFinal={completedFinal}".format(name=job_name, **metrics)
            )
    return "\n".join(lines)


def write_json(path: Path, data: Dict[str, Dict[str, Summary]]) -> None:
    payload = {session: {job: summary.to_dict() for job, summary in jobs.items()} for session, jobs in data.items()}
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def load_json(path: Path) -> Dict[str, Dict[str, Dict[str, float]]]:
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise SystemExit(f"Failed to parse JSON baseline '{path}': {exc}") from exc
    if not isinstance(raw, dict):
        raise SystemExit(f"Baseline file '{path}' must contain a JSON object at the root.")
    result: Dict[str, Dict[str, Dict[str, float]]] = {}
    for session, jobs in raw.items():
        if not isinstance(jobs, dict):
            raise SystemExit(f"Baseline session '{session}' must map to an object of job metrics.")
        session_map: Dict[str, Dict[str, float]] = {}
        for job, metrics in jobs.items():
            if not isinstance(metrics, dict):
                raise SystemExit(f"Baseline job '{job}' under session '{session}' must be an object of metrics.")
            metric_map: Dict[str, float] = {}
            for key, value in metrics.items():
                if not isinstance(value, (int, float)):
                    raise SystemExit(
                        f"Baseline metric '{session}.{job}.{key}' must be numeric (int or float)."
                    )
                metric_map[key] = float(value)
            session_map[job] = metric_map
        result[session] = session_map
    return result


def compare_against_baseline(
    current: Dict[str, Dict[str, Summary]],
    baseline: Dict[str, Dict[str, Dict[str, float]]],
    tolerance: float,
) -> Tuple[List[str], List[str]]:
    regressions: List[str] = []
    notes: List[str] = []

    penalty_metrics_upper = {"pendingAvg", "pendingMax", "activePeak"}
    penalty_metrics_lower = {"completedFinal"}

    for session, jobs in baseline.items():
        if session not in current:
            regressions.append(f"Baseline session '{session}' missing from current results.")
            continue
        for job_name, baseline_metrics in jobs.items():
            if job_name not in current[session]:
                regressions.append(
                    f"Baseline job '{job_name}' under session '{session}' missing from current results."
                )
                continue
            current_metrics = current[session][job_name].to_dict()
            for metric_name, baseline_value in baseline_metrics.items():
                if metric_name not in current_metrics:
                    notes.append(
                        f"Metric '{metric_name}' from baseline {session}.{job_name} not present in current metrics; skipping."
                    )
                    continue
                current_value = float(current_metrics[metric_name])
                if metric_name in penalty_metrics_upper:
                    allowed = baseline_value * (1.0 + tolerance)
                    if current_value > allowed:
                        regressions.append(
                            (
                                f"{session}.{job_name}.{metric_name}: {current_value:.3f} exceeds allowed "
                                f"{allowed:.3f} (baseline {baseline_value:.3f}, tolerance {tolerance:.0%})."
                            )
                        )
                elif metric_name in penalty_metrics_lower:
                    allowed = baseline_value * (1.0 - tolerance)
                    if current_value < allowed:
                        regressions.append(
                            (
                                f"{session}.{job_name}.{metric_name}: {current_value:.3f} below allowed "
                                f"{allowed:.3f} (baseline {baseline_value:.3f}, tolerance {tolerance:.0%})."
                            )
                        )
                else:
                    notes.append(
                        f"Metric '{metric_name}' has no regression rule; compare manually (baseline {baseline_value}, current {current_value})."
                    )
    return regressions, notes


def main() -> None:
    parser = argparse.ArgumentParser(description="Summarise job-system telemetry CSV exports.")
    parser.add_argument("telemetry_root", type=Path, help="Directory containing telemetry session folders")
    parser.add_argument("--json", type=Path, help="Optional path to write JSON summary")
    parser.add_argument("--baseline", type=Path, help="Optional baseline JSON file to compare against")
    parser.add_argument(
        "--regression-tolerance",
        type=float,
        default=0.05,
        help="Allowed fractional drift (e.g. 0.05 = 5%%) before a metric is considered a regression.",
    )
    parser.add_argument(
        "--fail-on-regression",
        action="store_true",
        help="Exit with code 1 if regressions against the baseline are detected.",
    )
    args = parser.parse_args()

    if not args.telemetry_root.exists():
        raise SystemExit(f"Telemetry root '{args.telemetry_root}' does not exist.")

    results = analyse_directory(args.telemetry_root)
    if not results:
        raise SystemExit("No job-system CSV exports found under telemetry root.")

    print(format_summary(results))

    if args.baseline is not None:
        if not args.baseline.exists():
            raise SystemExit(f"Baseline file '{args.baseline}' does not exist.")
        baseline_data = load_json(args.baseline)
        regressions, notes = compare_against_baseline(results, baseline_data, args.regression_tolerance)
        print("")
        print("Baseline comparison:")
        if regressions:
            for entry in regressions:
                print(f"  REGRESSION: {entry}")
        else:
            print("  No regressions detected against baseline.")
        if notes:
            print("  Notes:")
            for entry in notes:
                print(f"    {entry}")
        if regressions and args.fail_on_regression:
            raise SystemExit(1)

    if args.json is not None:
        write_json(args.json, results)
        print(f"\nJSON summary written to {args.json}")


if __name__ == "__main__":
    main()
