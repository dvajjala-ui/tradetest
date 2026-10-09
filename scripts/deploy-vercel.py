#!/usr/bin/env python3
"""Deploy the pushed Git commit through the authenticated Vercel CLI; no workspace files or keys are uploaded."""
import argparse
import json
from pathlib import Path
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scope', default='dvajjala-2765s-projects')
    parser.add_argument('--project', default='tradetest-dashboard')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]

    def git(*arguments):
        return subprocess.check_output(['git', *arguments], cwd=root, text=True).strip()

    if git('status', '--porcelain'):
        raise SystemExit('Commit and push the working changes before deploying.')
    remote = git('remote', 'get-url', 'origin')
    if remote not in ('https://github.com/dvajjala-ui/tradetest.git', 'https://github.com/dvajjala-ui/tradetest',
                      'git@github.com:dvajjala-ui/tradetest.git'):
        raise SystemExit('This helper is configured for the dvajjala-ui/tradetest repository.')
    subprocess.run(['git', 'fetch', 'origin', 'main'], cwd=root, check=True)
    commit = git('rev-parse', 'HEAD')
    if commit != git('rev-parse', 'origin/main'):
        raise SystemExit('The local commit must match the pushed main branch.')

    def request(endpoint, method='GET', body=None):
        command = ['npx', '--yes', 'vercel@63.1.0', 'api', endpoint, '--scope', args.scope,
                   '--method', method, '--raw', '--no-color']
        if body is not None:
            command += ['--input', '-']
        result = subprocess.run(command, cwd=root, input=json.dumps(body) if body is not None else None,
                                text=True, capture_output=True, timeout=45)
        try:
            data = json.loads(result.stdout)
        except json.JSONDecodeError:
            raise SystemExit('Vercel returned no JSON response. Check workspace login with `npx vercel whoami`.') from None
        if result.returncode or data.get('error'):
            error = data.get('error') or {}
            raise SystemExit(error.get('message', 'Vercel rejected the request.'))
        return data

    project = request('/v9/projects/' + args.project)
    if project.get('rootDirectory') != 'web' or project.get('framework') != 'vite':
        raise SystemExit('Configure this Vercel project with the web root directory and Vite first.')
    deployment = request('/v13/deployments', 'POST', {
        'name': args.project, 'project': project['id'], 'target': 'production',
        'gitSource': {'type': 'github', 'repoId': 1410084655, 'ref': 'main', 'sha': commit},
        'projectSettings': {'framework': 'vite', 'rootDirectory': 'web', 'nodeVersion': '24.x',
                            'installCommand': 'npm ci', 'buildCommand': 'npm run build', 'outputDirectory': 'dist'}
    })
    deployment_id = deployment['id']
    print(json.dumps({'deploymentId': deployment_id, 'commit': commit, 'state': deployment['readyState']}), flush=True)
    deadline, previous = time.monotonic() + 240, deployment['readyState']
    while deployment['readyState'] not in ('READY', 'ERROR', 'CANCELED'):
        if time.monotonic() >= deadline:
            raise SystemExit('Deployment is still building. Inspect ' + deployment_id + ' in Vercel.')
        time.sleep(2)
        deployment = request('/v13/deployments/' + deployment_id)
        if deployment['readyState'] != previous:
            previous = deployment['readyState']
            print(json.dumps({'state': previous}), flush=True)
    if deployment['readyState'] != 'READY':
        raise SystemExit(deployment.get('errorMessage') or 'The production build did not complete.')
    source_commit = (deployment.get('meta') or {}).get('githubCommitSha') or (deployment.get('gitSource') or {}).get('sha')
    if source_commit != commit:
        raise SystemExit('Production was built, but its source commit could not be verified.')
    aliases = deployment.get('alias') or []
    canonical = args.project + '.vercel.app'
    domain = canonical if canonical in aliases else aliases[0] if aliases else deployment['url']
    print(json.dumps({'productionUrl': 'https://' + domain, 'deploymentId': deployment_id,
                      'commit': source_commit, 'state': 'READY'}), flush=True)


if __name__ == '__main__':
    main()
