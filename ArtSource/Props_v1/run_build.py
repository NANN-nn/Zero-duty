import pathlib, runpy, traceback
folder = pathlib.Path(__file__).parent
try:
    runpy.run_path(str(folder / 'build_props.py'), run_name='__main__')
except BaseException:
    (folder / 'build_error.txt').write_text(traceback.format_exc(), encoding='utf-8')
    raise
